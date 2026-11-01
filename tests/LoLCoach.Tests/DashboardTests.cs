using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LoLCoach.Models;
using LoLCoach.Services;
using LoLCoach.ViewModels;
using Xunit;

namespace LoLCoach.Tests;

[CollectionDefinition("Desktop", DisableParallelization = true)]
public class DesktopCollection;

[Collection("Desktop")]
public class DashboardTests
{
    [Fact]
    public async Task DashboardStartsAutomaticallyAndShowsGameAndSeparateDiagnostics()
    {
        await RunStaAsync(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "LoLCoach-test-" + Guid.NewGuid());
            var store = new SettingsService(directory);
            store.Save(new AppSettings { RequestLimit = 1 }); store.SaveKey("fake-key-for-tests-only");
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var model = new Model();
            var reader = new Reader();
            var speech = new RecordingSpeech();
            var captureCalls = 0;
            var verifyAssets = Environment.GetEnvironmentVariable("LOLCOACH_TEST_ASSETS") == "1";
            var viewModel = new DashboardViewModel(store, verifyAssets,
                coachingClientFactory: (_, _, report) => { model.Report = report; return model; },
                practiceReaderFactory: (report, _) => { reader.Report = report; return Task.FromResult<IPracticeStateReader>(reader); },
                speechService: speech,
                observationSourceFactory: (_, _) => { captureCalls++; return Task.FromResult<IObservationSource>(new Source(captureCalls == 1 ? 500 : 200)); });
            viewModel.ContinuousSending = false;
            var window = new MainWindow(viewModel);
            window.Show();
            try
            {
                await RenderAsync();
                Assert.True(viewModel.AutomaticStartEnabled);
                Assert.Null(viewModel.SelectedChampion);
                Assert.Equal(173, viewModel.Champions.Length);
                Assert.Contains(viewModel.Champions, champion => champion.Id == "Akali");
                Assert.Equal(1, model.ProbeCalls);
                Assert.Equal(0, reader.Calls);
                Assert.Equal(0, captureCalls);
                Assert.Equal(5, window.Panels.Count);
                Assert.DoesNotContain(window.Panels.Values, panel => panel.Title == "Conexión");
                model.Reply.SetResult("Disponible.");
                await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await RenderAsync();
                Assert.Equal("Disponible", viewModel.ModelStatus);
                Assert.Equal("Disponible.", viewModel.ModelReply);
                Assert.True(viewModel.IsWaitingForGame);
                Assert.Equal(0, model.StreamCalls);
                window.Width = 760; await RenderAsync(); SaveScreenshot(window, "startup-wait-760");
                reader.Ready.SetResult(new(new(120, 3, 500, 800, 600), TestObservations.Practice, TimeProvider.System.GetTimestamp()));
                await model.StreamStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await RenderAsync();
                Assert.Contains(viewModel.ApiFields, field => field.Name == "Campeón" && field.Value == "Garen");
                Assert.Contains(viewModel.ApiFields, field => field.Name == "Oro disponible" && field.Value == "600");
                Assert.Contains(viewModel.ApiFields, field => field.Name == "Armadura" && field.Value == "40");
                Assert.Contains(viewModel.ApiFields, field => field.Name == "Daño de ataque" && field.Value == "75");
                Assert.Contains(viewModel.ApiFields, field => field.Name == "MANA" && field.Value == "200 / 300");
                Assert.Equal("Garen", viewModel.OwnChampion);
                Assert.Equal("Fase estimada: early", viewModel.GamePhase);
                Assert.Equal("Garen: Q para acercarte, W para resistir, E junto al rival y R para rematar.", viewModel.MiniGuide);
                Assert.NotNull(FindTextBlock(window, viewModel.MiniGuide));
                Assert.Equal(Visibility.Visible, Assert.IsType<Expander>(window.FindName("MiniGuidePanel")).Visibility);
                Assert.Equal(3, viewModel.Players.Count);
                Assert.Equal("Ashe · Rival", viewModel.Players[0].Heading);
                Assert.Equal("Garen · Tú", viewModel.Players[1].Heading);
                Assert.Equal("Lux · Aliado", viewModel.Players[2].Heading);
                Assert.Contains("2/1/3 K/D/A · 45 CS", viewModel.Players[1].Scores);
                Assert.Contains("Muerto (12 s)", viewModel.Players[0].Details);
                var levelValue = Assert.IsType<TextBlock>(window.FindName("OwnLevelValue"));
                Assert.Equal("3", levelValue.Text);
                Assert.Equal(FontWeights.Bold, levelValue.FontWeight);
                Assert.Equal(Color.FromRgb(20, 93, 160), Assert.IsType<SolidColorBrush>(levelValue.Foreground).Color);
                var healthValue = Assert.IsType<TextBlock>(window.FindName("OwnHealthValue"));
                Assert.Equal("500 / 800", healthValue.Text);
                Assert.Equal(FontWeights.Bold, healthValue.FontWeight);
                Assert.Equal(Color.FromRgb(32, 94, 79), Assert.IsType<SolidColorBrush>(healthValue.Foreground).Color);
                Assert.Equal("#205E4F", viewModel.ApiFields.Single(field => field.Name == "Vida").HighlightColor);
                Assert.NotNull(viewModel.Scene);
                Assert.Empty(speech.Spoken);
                model.Finish.SetResult();
                await viewModel.StartCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(3));
                await RenderAsync();
                Assert.Equal(1, viewModel.Requests); Assert.Equal(1, model.StreamCalls); Assert.Equal(1, captureCalls);
                Assert.Single(speech.Spoken); Assert.Equal(viewModel.Advice, speech.Spoken[0]);
                var firstMessage = Assert.Single(viewModel.Messages);
                Assert.Equal("Avanza.", firstMessage.Text);
                Assert.Equal("Completo", firstMessage.State);
                Assert.True(firstMessage.IsLatest);
                Assert.Empty(model.StreamContexts[0]);
                var firstAdviceText = Assert.IsType<TextBlock>(FindTextBlock(window, firstMessage.Text));
                Assert.Equal(54, firstAdviceText.FontSize);
                Assert.Equal(67.5, firstAdviceText.LineHeight);
                Assert.Equal(Color.FromRgb(20, 93, 160), Assert.IsType<SolidColorBrush>(firstAdviceText.Foreground).Color);
                Assert.NotEmpty(viewModel.LolDiagnostics); Assert.NotEmpty(viewModel.ModelDiagnostics);
                Assert.All(viewModel.LolDiagnostics, entry => Assert.Equal("LoL", entry.Api));
                Assert.All(viewModel.ModelDiagnostics, entry => Assert.Equal("Azure OpenAI", entry.Api));
                Assert.DoesNotContain("fake-key", JsonSerializer.Serialize(viewModel.ModelDiagnostics));
                await viewModel.InitializeAsync();
                Assert.Equal(1, model.ProbeCalls);
                window.Width = 1120; await RenderAsync(); SaveScreenshot(window, "dashboard-1120");
                SaveScreenshot(window, "game-data-1120");
                window.Width = 760; await RenderAsync(); SaveScreenshot(window, "game-data-760");
                SaveScreenshot(window, "api-debug-760");
                for (var index = 0; index < 250; index++) model.Report!(new(DateTimeOffset.UtcNow, "Azure OpenAI", "POST", "/openai/v1/chat/completions", 200, 20, "Correcto"));
                Assert.Equal(200, viewModel.ModelDiagnostics.Count);
                viewModel.ClearDiagnosticsCommand.Execute(null);
                Assert.Empty(viewModel.LolDiagnostics); Assert.Empty(viewModel.ModelDiagnostics);
                viewModel.StopCommand.Execute(null); Assert.Empty(viewModel.Advice);
                Assert.Same(firstMessage, Assert.Single(viewModel.Messages));
                Assert.Equal("Avanza.", viewModel.Messages[0].Text);
                await viewModel.StartCommand.ExecuteAsync(null);
                await RenderAsync();
                Assert.Equal(2, viewModel.Messages.Count);
                Assert.Same(firstMessage, viewModel.Messages[0]);
                Assert.Equal("Completo", viewModel.Messages[1].State);
                Assert.Equal("200 / 800", healthValue.Text);
                Assert.Equal(Color.FromRgb(172, 69, 57), Assert.IsType<SolidColorBrush>(healthValue.Foreground).Color);
                Assert.Equal("#AC4539", viewModel.ApiFields.Single(field => field.Name == "Vida").HighlightColor);
                Assert.False(firstMessage.IsLatest);
                Assert.True(viewModel.Messages[1].IsLatest);
                Assert.Equal(new[] { "Avanza." }, model.StreamContexts[1]);
                var latestAdviceText = Assert.IsType<TextBlock>(FindTextBlock(window, "Retrocede."));
                Assert.Equal(54, latestAdviceText.FontSize);
                Assert.Equal(Color.FromRgb(20, 93, 160), Assert.IsType<SolidColorBrush>(latestAdviceText.Foreground).Color);
                window.Width = 760; await RenderAsync(); SaveScreenshot(window, "advice-history-760");
                window.Width = 1120; await RenderAsync(); SaveScreenshot(window, "advice-history-1120");
                var columns = FindAll<Border>(window.ChampionsPanel).Where(border => border.DataContext is PlayerStatsRow && border.Name == "Column").ToList();
                Assert.Equal("Rival", ((PlayerStatsRow)columns[0].DataContext).Player.Relation);
                Assert.True(columns[1].TranslatePoint(new Point(), window.ChampionsPanel).X > columns[0].TranslatePoint(new Point(), window.ChampionsPanel).X);
                Assert.NotNull(FindTextBlock(window, "Ashe · Rival"));
                var enemyScores = Assert.IsType<TextBlock>(FindTextBlock(window, "", block =>
                    block.DataContext is PlayerStatsRow row && row.Player.Relation == "Rival"
                    && block.Inlines.OfType<System.Windows.Documents.Run>().Any(inline => inline.FontWeight == FontWeights.Bold)));
                var enemyLevel = Assert.Single(enemyScores.Inlines.OfType<System.Windows.Documents.Run>(), inline => inline.FontWeight == FontWeights.Bold);
                Assert.Equal("4", enemyLevel.Text);
                Assert.Equal(Color.FromRgb(20, 93, 160), Assert.IsType<SolidColorBrush>(enemyLevel.Foreground).Color);
                var attributes = Assert.IsType<Expander>(window.FindName("OwnAttributes"));
                Assert.True(attributes.IsExpanded);
                Assert.NotNull(FindTextBlock(window, "Armadura"));
                SaveScreenshot(window, "champion-attributes-1120");
                attributes.IsExpanded = false;
                for (var index = 0; index < 7; index++)
                    viewModel.Players.Add(new(new("Annie", "Rival", false, 4, "MIDDLE", 1, 1, 1, 20, 0, false, 0, [])));
                await RenderAsync();
                var playersScroll = Assert.IsType<ScrollViewer>(window.FindName("PlayersScroll"));
                Assert.Equal(10, viewModel.Players.Count);
                Assert.True(playersScroll.ScrollableWidth > 0);
                playersScroll.ScrollToRightEnd(); await RenderAsync();
                Assert.True(playersScroll.HorizontalOffset >= playersScroll.ScrollableWidth - 1);
                window.Width = 760; await RenderAsync();
                Assert.All(window.Panels.Values, panel => Assert.True(panel.Left <= window.Workspace.ActualWidth - 80));
                Assert.True(window.ChampionsPanel.ActualWidth <= window.Workspace.ActualWidth);
                SaveScreenshot(window, "champion-stats-760");
                window.Width = 1120; await RenderAsync();
                for (var index = 0; index < 40; index++)
                    viewModel.AppendMessage(new AdviceMessage { Text = $"Consejo de prueba {index + 3}: compara tu vida y el oro disponible antes de decidir.", State = "Completo" });
                await RenderAsync();
                var historyScroll = Assert.IsType<ScrollViewer>(window.FindName("AdviceHistoryScroll"));
                Assert.True(historyScroll.ScrollableHeight > 0);
                Assert.True(historyScroll.VerticalOffset >= historyScroll.ScrollableHeight - 1);
                Assert.Equal(DashboardViewModel.MaxMessages, viewModel.Messages.Count);
                Assert.DoesNotContain(firstMessage, viewModel.Messages);
                Assert.StartsWith("Consejo de prueba 13:", viewModel.Messages[0].Text);
                SaveScreenshot(window, "advice-history-scrolled-1120");

                window.RecommendationPanel.IsOpen = true; await RenderAsync();
                for (var index = 0; index < 8; index++)
                {
                    viewModel.CurrentQuestion.SelectedIndex = 0;
                    await viewModel.NextQuestionCommand.ExecuteAsync(null);
                }
                Assert.Equal(3, viewModel.Recommendations.Count);
                if (verifyAssets) Assert.All(viewModel.Recommendations, champion => Assert.NotNull(champion.Portrait));
                viewModel.SaveProfileCommand.Execute(null); Assert.NotNull(store.Load().Profile);
                using var capture = new WindowCaptureService();
                capture.Start(new(new WindowInteropHelper(window).Handle, window.Title));
                await WaitForPreviewAsync(capture);
                Assert.NotNull(capture.GetPreview()); Assert.False(capture.IsGameWindow);

                var pendingReader = new Reader();
                var resumedModel = new Model(); resumedModel.Reply.SetResult("Disponible.");
                var pending = new DashboardViewModel(store, false,
                    (_, _, report) => { resumedModel.Report = report; return resumedModel; },
                    (report, _) => { pendingReader.Report = report; return Task.FromResult<IPracticeStateReader>(pendingReader); },
                    new RecordingSpeech());
                try
                {
                    var waiting = pending.InitializeAsync();
                    await pendingReader.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
                    pending.StopCommand.Execute(null); await waiting;
                    Assert.False(pending.IsBusy); Assert.False(pending.AutomaticStartEnabled);
                    Assert.Equal(0, resumedModel.StreamCalls);
                }
                finally { await pending.ShutdownAsync(); }
                await VerifyFloatingWindowsAsync(store);
            }
            finally
            {
                window.Close(); await RenderAsync(); store.Reset(); application.Shutdown();
            }
        });
    }

    private static async Task VerifyFloatingWindowsAsync(SettingsService store)
    {
        var window = new MainWindow(new DashboardViewModel(store, false, speechService: new RecordingSpeech()), false);
        MainWindow? restored = null;
        var closed = false;
        window.Show();
        try
        {
            await RenderAsync();
            var panel = window.TrainingPanel;
            var thumbs = FindAll<Thumb>(panel).ToList();
            thumbs.Single(thumb => thumb.Name == "PART_TitleBar").RaiseEvent(new DragDeltaEventArgs(40, 30));
            thumbs.Single(thumb => Equals(thumb.Tag, "BottomRight")).RaiseEvent(new DragDeltaEventArgs(50, 40));
            thumbs.Single(thumb => Equals(thumb.Tag, "Left")).RaiseEvent(new DragDeltaEventArgs(-20, 0));
            await RenderAsync();
            Assert.Equal(new PanelLayout(366, 38, 510, 440, true), panel.GetLayout() with { ZIndex = 0 });
            thumbs.Single(thumb => thumb.Name == "PART_TitleBar").RaiseEvent(new DragDeltaEventArgs(-5000, -5000));
            Assert.True(panel.Left + panel.Width >= 80); Assert.Equal(0, panel.Top);
            var expected = panel.GetLayout();
            FindAll<ButtonBase>(panel).Single(button => button.Name == "PART_Close").RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            await RenderAsync();
            Assert.False(panel.IsOpen); Assert.Equal(Visibility.Collapsed, panel.Visibility);
            var menuItem = FindLogical<MenuItem>(window.MainMenu).Single(item => Equals(item.Header, "Entrenamiento"));
            Assert.False(menuItem.IsChecked);
            window.Close(); closed = true; await RenderAsync();

            restored = new MainWindow(new DashboardViewModel(store, false, speechService: new RecordingSpeech()), false);
            restored.Show(); await RenderAsync();
            var layout = restored.TrainingPanel.GetLayout();
            Assert.False(layout.IsOpen);
            Assert.Equal((expected.Left, expected.Top, expected.Width, expected.Height), (layout.Left, layout.Top, layout.Width, layout.Height));
            FindLogical<MenuItem>(restored.MainMenu).Single(item => Equals(item.Header, "Entrenamiento")).IsChecked = true;
            await RenderAsync();
            Assert.True(restored.TrainingPanel.IsOpen); Assert.Equal(Visibility.Visible, restored.TrainingPanel.Visibility);
            Assert.True(Panel.GetZIndex(restored.TrainingPanel) > restored.Panels.Values.Where(item => item != restored.TrainingPanel).Max(Panel.GetZIndex));
        }
        finally
        {
            if (!closed) window.Close();
            restored?.Close(); await RenderAsync();
        }
    }

    private sealed class RecordingSpeech : ISpeechService
    {
        public string[] Voices { get; } = ["Voz española de prueba"];
        public string? InitializationError => null;
        public List<string> Spoken { get; } = [];
        public void Speak(string text, string? voice, int volume) => Spoken.Add(text);
        public Task WaitUntilFinishedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public void Cancel() { }
        public void Dispose() { }
    }
    private sealed class Reader : IPracticeStateReader
    {
        public Action<ApiDiagnostic>? Report { get; set; }
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<PracticeState> Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public async Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken)
        {
            Calls++; Started.TrySetResult();
            var state = await Ready.Task.WaitAsync(cancellationToken);
            Report?.Invoke(new(DateTimeOffset.UtcNow, "LoL", "GET", "/liveclientdata/activeplayer", 200, 10, "Correcto"));
            return state;
        }
    }
    private sealed class Model : IModelClient, IChampionGuideClient
    {
        public Action<ApiDiagnostic>? Report { get; set; }
        public TaskCompletionSource<string> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource StreamStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finish { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int ProbeCalls { get; private set; }
        public int StreamCalls { get; private set; }
        public List<string[]> StreamContexts { get; } = [];
        public Task<string> CreateGuideAsync(PracticeState state, PlayerProfile profile, CancellationToken cancellationToken)
            => Task.FromResult("Garen: Q para acercarte, W para resistir, E junto al rival y R para rematar.");
        public async Task<string> ProbeAsync(CancellationToken cancellationToken)
        {
            ProbeCalls++;
            var result = await Reply.Task.WaitAsync(cancellationToken);
            Report?.Invoke(new(DateTimeOffset.UtcNow, "Azure OpenAI", "POST", "/openai/v1/chat/completions", 200, 50, "Correcto"));
            return result;
        }
        public async IAsyncEnumerable<string> StreamAsync(Observation observation, PlayerProfile profile,
            IReadOnlyList<string> recentAdvice, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            StreamCalls++;
            StreamContexts.Add(recentAdvice.ToArray());
            var instruction = StreamCalls == 1 ? "Avanza" : "Retrocede";
            StreamStarted.TrySetResult(); yield return instruction;
            await Finish.Task.WaitAsync(cancellationToken);
            yield return ".";
        }
    }
    private sealed class Source(double health = 500) : IObservationSource
    {
        public Task<Observation> CaptureAsync(CancellationToken cancellationToken)
        {
            var pixels = Enumerable.Repeat((byte)240, 400 * 240 * 4).ToArray();
            var image = WindowCaptureService.CreateFrame(400, 240, pixels, 1600);
            var snapshot = new GameSnapshot(120, 3, health, 800, 600)
            {
                Player = new PlayerGameDetails { ResourceType = "MANA", Resource = 200, MaxResource = 300,
                    CombatStats = new() { ["armor"] = 40, ["attackDamage"] = 75 } },
                Roster = [new("Garen", "Tú", true, 3, "TOP", 2, 1, 3, 45, 1.5, false, 0, [new("Espada de Doran", 1)]),
                    new("Lux", "Aliado", false, 4, "MIDDLE", 1, 0, 2, 35, 2, false, 0, []),
                    new("Ashe", "Rival", false, 4, "BOTTOM", 0, 2, 1, 25, 0, true, 12, [new("Botas", 1)])]
            };
            return Task.FromResult(TestObservations.Create(snapshot, TimeProvider.System.GetTimestamp(), ImageCodec.Encode(image)));
        }
    }
    private static Task RunStaAsync(Func<Task> action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.BeginInvoke(async () =>
            {
                try { await action(); completion.SetResult(); }
                catch (Exception exception) { completion.SetException(exception); }
                finally { if (!dispatcher.HasShutdownStarted) dispatcher.InvokeShutdown(); }
            });
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
    }
    private static async Task RenderAsync() => await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
    private static IEnumerable<T> FindLogical<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) yield return match;
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            foreach (var found in FindLogical<T>(child)) yield return found;
    }
    private static IEnumerable<T> FindAll<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent is T match) yield return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            foreach (var child in FindAll<T>(VisualTreeHelper.GetChild(parent, index))) yield return child;
    }
    private static TextBlock? FindTextBlock(DependencyObject parent, string text, Func<TextBlock, bool>? match = null)
    {
        if (parent is TextBlock block && (match?.Invoke(block) ?? block.Text == text)) return block;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindTextBlock(VisualTreeHelper.GetChild(parent, index), text, match) is { } found) return found;
        return null;
    }
    private static Task WaitForPreviewAsync(WindowCaptureService capture)
    {
        var completion = new TaskCompletionSource(); var deadline = DateTime.UtcNow.AddSeconds(8);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            if (capture.GetPreview() is not null) { timer.Stop(); completion.TrySetResult(); }
            else if (DateTime.UtcNow >= deadline || capture.Error is not null)
            { timer.Stop(); completion.TrySetException(new InvalidOperationException(capture.Error ?? "No frame.")); }
        };
        timer.Start(); return completion.Task;
    }
    private static void SaveScreenshot(Window window, string name)
    {
        window.UpdateLayout();
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "LoLCoach.slnx"))) repository = repository.Parent;
        var root = Path.Combine(repository?.FullName ?? throw new InvalidOperationException("Repository not found."), "artifacts");
        Directory.CreateDirectory(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var output = File.Create(Path.Combine(root, name + ".png")); encoder.Save(output);
    }
}
