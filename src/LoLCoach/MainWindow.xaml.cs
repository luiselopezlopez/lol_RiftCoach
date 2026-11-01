using System.ComponentModel;
using System.Windows;
using LoLCoach.ViewModels;

namespace LoLCoach;

public partial class MainWindow : Window
{
    private readonly DashboardViewModel viewModel;
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
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (startAutomatically) await viewModel.InitializeAsync();
    }

    private void OnHistorySizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.NewSize.Height == args.PreviousSize.Height) return;
        _ = Dispatcher.BeginInvoke(new Action(AdviceHistoryScroll.ScrollToEnd), System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnTrainingSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (PlayerStatsPanel is null || StatsColumn is null) return;
        var compact = args.NewSize.Width < 980;
        StatsColumn.Width = new GridLength(compact ? 0 : 310);
        System.Windows.Controls.Grid.SetColumn(PlayerStatsPanel, compact ? 0 : 1);
        System.Windows.Controls.Grid.SetRow(PlayerStatsPanel, compact ? 1 : 0);
        System.Windows.Controls.Grid.SetColumnSpan(PlayerStatsPanel, compact ? 3 : 1);
    }

    private async void OnClosing(object? sender, CancelEventArgs args)
    {
        if (closing) return;
        args.Cancel = true;
        if (shutdownStarted) return;
        shutdownStarted = true;
        await viewModel.ShutdownAsync();
        closing = true;
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}