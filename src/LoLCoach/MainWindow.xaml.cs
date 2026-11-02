using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LoLCoach.Controls;
using LoLCoach.Services;
using LoLCoach.ViewModels;

namespace LoLCoach;

public partial class MainWindow : Window
{
    private readonly DashboardViewModel viewModel;
    private readonly Dictionary<string, FloatingPanel> panels;
    private readonly Dictionary<string, PanelLayout> defaultLayout;
    private bool closing;
    private bool shutdownStarted;
    private readonly bool startAutomatically;

    public MainWindow() : this(new DashboardViewModel()) { }

    public MainWindow(DashboardViewModel viewModel, bool startAutomatically = true)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        DataContext = this.viewModel;
        this.startAutomatically = startAutomatically;
        panels = new()
        {
            ["champion"] = ChampionPanel, ["training"] = TrainingPanel, ["champions"] = ChampionsPanel,
            ["session"] = SessionPanel, ["recommendation"] = RecommendationPanel
        };
        defaultLayout = panels.Select((entry, index) => (entry.Key, Layout: entry.Value.GetLayout() with { ZIndex = index + 1 }))
            .ToDictionary(entry => entry.Key, entry => entry.Layout);
        viewModel.ProfileSaved += (_, _) => { TrainingPanel.IsOpen = true; TrainingPanel.BringToFront(); };
        Loaded += OnLoaded;
    }

    public IReadOnlyDictionary<string, FloatingPanel> Panels => panels;

    private void OnOpenSettings(object sender, RoutedEventArgs args)
        => new SettingsWindow(viewModel) { Owner = this }.ShowDialog();

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        ApplyLayout(viewModel.LoadPanelLayout());
        if (startAutomatically) await viewModel.InitializeAsync();
    }

    private void ApplyLayout(IReadOnlyDictionary<string, PanelLayout> saved)
    {
        foreach (var (id, panel) in panels)
        {
            if (saved.TryGetValue(id, out var layout)) { panel.ApplyLayout(layout); continue; }
            var fallback = defaultLayout[id];
            if (id == "recommendation") fallback = fallback with { IsOpen = viewModel.SelectedChampion is null };
            panel.ApplyLayout(fallback);
        }
    }

    public IReadOnlyDictionary<string, PanelLayout> CaptureLayout()
    {
        var order = panels.OrderBy(entry => System.Windows.Controls.Panel.GetZIndex(entry.Value)).Select(entry => entry.Key).ToList();
        return panels.ToDictionary(entry => entry.Key, entry => entry.Value.GetLayout() with { ZIndex = order.IndexOf(entry.Key) + 1 });
    }

    /// <summary>Matches the native title bar to the dark hextech theme (Windows 10 20H1+ / Windows 11).</summary>
    private void OnSourceInitialized(object? sender, EventArgs args)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var dark = 1;
        _ = DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
        var caption = 0x00130A01; // COLORREF (BGR) for #010A13
        _ = DwmSetWindowAttribute(handle, DwmCaptionColor, ref caption, sizeof(int));
    }

    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void OnResetLayout(object sender, RoutedEventArgs args)
    {
        foreach (var (id, panel) in panels) panel.ApplyLayout(defaultLayout[id] with { IsOpen = true });
    }

    private void OnWorkspaceSizeChanged(object sender, SizeChangedEventArgs args)
    {
        foreach (var panel in panels.Values.Where(panel => panel.IsOpen)) panel.KeepInside();
    }

    private void OnHistorySizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.NewSize.Height == args.PreviousSize.Height) return;
        _ = Dispatcher.BeginInvoke(new Action(AdviceHistoryScroll.ScrollToEnd), System.Windows.Threading.DispatcherPriority.Background);
    }

    private async void OnClosing(object? sender, CancelEventArgs args)
    {
        if (closing) return;
        args.Cancel = true;
        if (shutdownStarted) return;
        shutdownStarted = true;
        viewModel.SavePanelLayout(CaptureLayout());
        await viewModel.ShutdownAsync();
        closing = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}
