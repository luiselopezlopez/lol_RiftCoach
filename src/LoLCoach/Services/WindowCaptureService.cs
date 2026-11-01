using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LoLCoach.Models;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace LoLCoach.Services;

public sealed record CaptureWindow(nint Handle, string Title)
{
    public override string ToString() => Title;
}

public sealed record CapturedWindowFrame(BitmapSource Image, long CapturedAt);

public interface IPracticeFrameSource
{
    bool IsGameWindow { get; }
    Task<CapturedWindowFrame> CaptureFreshAsync(CancellationToken cancellationToken);
}

public sealed class WindowCaptureService : IPracticeFrameSource, IDisposable
{
    private readonly object sync = new();
    private ID3D11Device? device;
    private ID3D11DeviceContext? context;
    private IDirect3DDevice? winrtDevice;
    private Direct3D11CaptureFramePool? pool;
    private GraphicsCaptureSession? session;
    private GraphicsCaptureItem? item;
    private nint window;
    private BitmapSource? latest;
    private long capturedAt;
    private uint processId;
    private TaskCompletionSource<CapturedWindowFrame>? pendingFrame;
    public bool IsGameWindow { get; private set; }
    public nint WindowHandle { get { lock (sync) return window; } }
    public string? Error { get; private set; }
    public static bool IsSupported => GraphicsCaptureSession.IsSupported();

    public static IReadOnlyList<CaptureWindow> GetWindows()
    {
        var windows = new List<CaptureWindow>();
        EnumWindows((handle, _) =>
        {
            var title = new StringBuilder(512);
            GetWindowText(handle, title, title.Capacity);
            if (IsWindowVisible(handle) && !IsIconic(handle) && title.Length > 0)
                windows.Add(new(handle, title.ToString()));
            return true;
        }, 0);
        return windows.OrderBy(entry => entry.Title).ToArray();
    }

    public static CaptureWindow? FindGameWindow()
    {
        var matches = GetWindows().Where(entry =>
        {
            try
            {
                GetWindowThreadProcessId(entry.Handle, out var owner);
                using var process = System.Diagnostics.Process.GetProcessById(checked((int)owner));
                return process.ProcessName.Equals("League of Legends", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException
                or System.ComponentModel.Win32Exception) { return false; }
        }).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    public void Start(CaptureWindow selected)
    {
        Dispose();
        if (!IsSupported) throw new InvalidOperationException("Este equipo no admite Windows Graphics Capture.");
        if (!IsWindowVisible(selected.Handle) || IsIconic(selected.Handle))
            throw new InvalidOperationException("Selecciona una ventana visible y no minimizada.");
        window = selected.Handle;
        GetWindowThreadProcessId(window, out processId);
        using (var process = System.Diagnostics.Process.GetProcessById(checked((int)processId)))
            IsGameWindow = process.ProcessName.Equals("League of Legends", StringComparison.OrdinalIgnoreCase);
        Marshal.ThrowExceptionForHR(D3D11CreateDevice(0, 1, 0, 0x20, 0, 0, 7,
            out var devicePointer, out _, out var contextPointer));
        device = new ID3D11Device(devicePointer);
        context = new ID3D11DeviceContext(contextPointer);
        using (var dxgi = device.QueryInterface<IDXGIDevice>())
        {
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var pointer));
            try { winrtDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pointer); }
            finally { Marshal.Release(pointer); }
        }
        item = CreateItem(window);
        pool = Direct3D11CaptureFramePool.CreateFreeThreaded(winrtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
        pool.FrameArrived += FrameArrived;
        session = pool.CreateCaptureSession(item);
        session.IsCursorCaptureEnabled = false;
        session.StartCapture();
    }

    public BitmapSource? GetPreview()
    {
        lock (sync)
        {
            if (!IsWindowVisible(window) || IsIconic(window) || latest is null
                || TimeProvider.System.GetElapsedTime(capturedAt) > TimeSpan.FromSeconds(6)) return null;
            return latest;
        }
    }

    public async Task<CapturedWindowFrame> CaptureFreshAsync(CancellationToken cancellationToken)
    {
        TaskCompletionSource<CapturedWindowFrame> request;
        lock (sync)
        {
            GetWindowThreadProcessId(window, out var currentProcessId);
            if (!IsGameWindow || currentProcessId != processId || session is null || !IsWindowVisible(window) || IsIconic(window))
                throw new PracticeUnavailableException("La ventana de League of Legends no está visible. Reanuda la captura.");
            request = new(TaskCreationOptions.RunContinuationsAsynchronously);
            if (pendingFrame is not null) throw new InvalidOperationException("Ya hay una captura pendiente.");
            pendingFrame = request;
        }
        try { return await request.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken); }
        catch (TimeoutException) { throw new PracticeUnavailableException("No ha llegado una captura nueva del juego; sesión detenida."); }
        finally
        {
            lock (sync) { if (pendingFrame == request) pendingFrame = null; }
        }
    }

    private void FrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        lock (sync)
        {
            if (device is null || context is null) return;
            try
            {
                using var frame = sender.TryGetNextFrame();
                if (frame is null || frame.ContentSize.Width <= 0 || frame.ContentSize.Height <= 0) return;
                if (pendingFrame is null && latest is not null && TimeProvider.System.GetElapsedTime(capturedAt) < TimeSpan.FromSeconds(5)) return;
                var frameTime = TimeProvider.System.GetTimestamp();
                var textureId = typeof(ID3D11Texture2D).GUID;
                frame.Surface.As<IDirect3DDxgiInterfaceAccess>().GetInterface(ref textureId, out var pointer);
                using var texture = new ID3D11Texture2D(pointer);
                var description = texture.Description;
                description.Usage = ResourceUsage.Staging;
                description.BindFlags = BindFlags.None;
                description.CPUAccessFlags = CpuAccessFlags.Read;
                description.MiscFlags = ResourceOptionFlags.None;
                using var staging = device.CreateTexture2D(description);
                context.CopyResource(staging, texture);
                context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None, out var mapped).CheckError();
                try
                {
                    var width = Math.Min((int)description.Width, frame.ContentSize.Width);
                    var height = Math.Min((int)description.Height, frame.ContentSize.Height);
                    var stride = checked(width * 4);
                    var pixels = new byte[checked(stride * height)];
                    for (var row = 0; row < height; row++)
                        Marshal.Copy(mapped.DataPointer + checked(row * (int)mapped.RowPitch), pixels, row * stride, stride);
                    latest = CreateFrame(width, height, pixels, stride);
                    capturedAt = frameTime;
                    Error = null;
                    pendingFrame?.TrySetResult(new(latest, capturedAt));
                    pendingFrame = null;
                }
                finally { context.Unmap(staging, 0); }
                if (item is not null && (frame.ContentSize.Width != description.Width || frame.ContentSize.Height != description.Height))
                    sender.Recreate(winrtDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
            }
            catch (Exception exception) when (exception is COMException or InvalidOperationException or ArgumentException)
            {
                latest = null;
                Error = "Captura no disponible. Vuelve a seleccionar la ventana.";
                pendingFrame?.TrySetException(new PracticeUnavailableException(Error));
                pendingFrame = null;
            }
        }
    }

    public static BitmapSource CreateFrame(int width, int height, byte[] pixels, int stride)
    {
        if (width < 320 || height < 180 || pixels.Length < checked(stride * height) || stride < checked(width * 4))
            throw new ArgumentException("Dimensiones de captura no válidas.");
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static GraphicsCaptureItem CreateItem(nint handle)
    {
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var hstring));
        nint factoryPointer = 0;
        try
        {
            var factoryId = typeof(IGraphicsCaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, ref factoryId, out factoryPointer));
            var factory = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPointer);
            try
            {
                var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
                factory.CreateForWindow(handle, ref itemId, out var itemPointer);
                try { return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer); }
                finally { Marshal.Release(itemPointer); }
            }
            finally { Marshal.ReleaseComObject(factory); }
        }
        finally
        {
            if (factoryPointer != 0) Marshal.Release(factoryPointer);
            WindowsDeleteString(hstring);
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            pendingFrame?.TrySetException(new PracticeUnavailableException("Captura detenida; no se enviarán más imágenes."));
            pendingFrame = null;
            if (pool is not null) pool.FrameArrived -= FrameArrived;
            session?.Dispose(); session = null;
            pool?.Dispose(); pool = null;
            item = null;
            winrtDevice?.Dispose(); winrtDevice = null;
            context?.Dispose(); context = null;
            device?.Dispose(); device = null;
            latest = null; window = 0; Error = null; IsGameWindow = false; processId = 0;
        }
    }

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        void GetInterface(ref Guid interfaceId, out nint pointer);
    }

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        void CreateForWindow(nint handle, ref Guid interfaceId, out nint result);
        void CreateForMonitor(nint monitor, ref Guid interfaceId, out nint result);
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(nint adapter, int driverType, nint software,
        uint flags, nint featureLevels, uint featureLevelCount, uint sdkVersion,
        out nint device, out int featureLevel, out nint context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint device, out nint graphicsDevice);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string text, int length, out nint hstring);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(nint hstring);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(nint className, ref Guid interfaceId, out nint factory);
}