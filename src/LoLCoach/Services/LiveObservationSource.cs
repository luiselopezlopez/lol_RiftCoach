using System.Windows.Media;
using System.Windows.Media.Imaging;
using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed class LiveObservationSource(IPracticeFrameSource capture, IPracticeStateReader game,
    TimeProvider? timeProvider = null) : IObservationSource
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<Observation> CaptureAsync(CancellationToken cancellationToken)
    {
        if (!capture.IsGameWindow) throw new PracticeUnavailableException("Selecciona la ventana del juego, no el cliente ni otra aplicación.");
        using var sampling = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var stateTask = game.ReadPracticeAsync(sampling.Token);
        var frameTask = capture.CaptureFreshAsync(sampling.Token);
        try { await Task.WhenAll(stateTask, frameTask); }
        catch { await sampling.CancelAsync(); throw; }
        cancellationToken.ThrowIfCancellationRequested();
        if (!capture.IsGameWindow) throw new PracticeUnavailableException("La captura del juego se ha detenido; no se enviará esta observación.");
        var state = await stateTask;
        var frame = await frameTask;
        var frameAge = clock.GetElapsedTime(frame.CapturedAt);
        var stateAge = clock.GetElapsedTime(state.ReadAt);
        if (frameAge < TimeSpan.Zero || stateAge < TimeSpan.Zero || frameAge > TimeSpan.FromSeconds(2)
            || stateAge > TimeSpan.FromSeconds(2) || (frameAge - stateAge).Duration() > TimeSpan.FromSeconds(1))
            throw new PracticeUnavailableException("Captura y estado no están sincronizados; no se enviará la observación.");
        var bitmap = frame.Image;
        if (!HasVisibleContent(bitmap)) throw new PracticeUnavailableException("La captura está vacía o negra; comprueba el modo de ventana.");
        if (bitmap.PixelWidth > 1280)
        {
            var scale = 1280.0 / bitmap.PixelWidth;
            bitmap = new TransformedBitmap(bitmap, new ScaleTransform(scale, scale));
            bitmap.Freeze();
        }
        var observation = new Observation(ImageCodec.Encode(bitmap), state.Snapshot, frame.CapturedAt, ObservationKind.BotPractice)
        { Practice = state.Verification };
        ProductPolicy.EnsureAllowed(observation);
        return observation;
    }

    private static bool HasVisibleContent(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = checked(converted.PixelWidth * 4);
        var pixels = new byte[checked(stride * converted.PixelHeight)];
        converted.CopyPixels(pixels, stride, 0);
        var samples = 0;
        for (var offset = 0; offset < pixels.Length; offset += 64)
            if (pixels[offset] > 48 || pixels[offset + 1] > 48 || pixels[offset + 2] > 48) samples++;
        return samples >= 20;
    }
}