using System.Collections.ObjectModel;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LoLCoach.Models;
using LoLCoach.Services;
using System.ClientModel;

namespace LoLCoach.ViewModels;

public partial class QuestionViewModel(QuizQuestion question) : ObservableObject
{
    public QuizQuestion Question { get; } = question;
    [ObservableProperty] private int selectedIndex = -1;
}

public partial class ChampionViewModel(ChampionChoice choice) : ObservableObject
{
    public ChampionChoice Choice { get; } = choice;
    public string Role => QuestionnaireService.RoleName(Choice.Role);
    [ObservableProperty] private ImageSource? portrait;
}

public sealed record ApiField(string Name, string Value, string? HighlightColor = null)
{
    public bool IsHighlighted => HighlightColor is not null;
}

public sealed record PlayerStatsRow(RosterPlayer Player)
{
    public string Heading => $"{Player.Champion} · {Player.Relation}";
    public string Scores => $"Nv. {Player.Level?.ToString() ?? "-"} · {Player.Kills?.ToString() ?? "-"}/{Player.Deaths?.ToString() ?? "-"}/{Player.Assists?.ToString() ?? "-"} K/D/A · {Player.CreepScore?.ToString() ?? "-"} CS";
    public string ScoreDetails => $" · {Player.Kills?.ToString() ?? "-"}/{Player.Deaths?.ToString() ?? "-"}/{Player.Assists?.ToString() ?? "-"} K/D/A · {Player.CreepScore?.ToString() ?? "-"} CS";
    public string Items => Player.Items.Count == 0 ? "Sin objetos" : string.Join(", ", Player.Items.Select(item => item.Count > 1 ? $"{item.Name} ×{item.Count}" : item.Name));
    public string Details => $"{Player.Position ?? "Sin posición"} · Visión: {Player.WardScore?.ToString("0.#") ?? "-"}" +
        (Player.IsDead ? $" · Muerto ({Player.RespawnTimer?.ToString("0") ?? "-"} s)" : "");
}

public partial class AdviceMessage : ObservableObject
{
    public DateTimeOffset ReceivedAt { get; } = DateTimeOffset.Now;
    public string TimeLabel => ReceivedAt.ToString("HH:mm:ss");
    [ObservableProperty] private string text = "";
    [ObservableProperty] private string state = "Borrador";
    [ObservableProperty] private bool isLatest;
}

public partial class DashboardViewModel : ObservableObject, IDisposable
{
    public const int MaxMessages = 30;
    private readonly SettingsService store;
    private readonly ISpeechService speech;
    private readonly WindowCaptureService capture = new();
    private readonly DataDragonService catalog = new();
    private readonly DispatcherTimer timer;
    private readonly CancellationTokenSource lifetime = new();
    private readonly bool loadCatalog;
    private readonly Func<AppSettings, string, Action<ApiDiagnostic>, IModelClient> createModel;
    private readonly Func<Action<ApiDiagnostic>, CancellationToken, Task<IPracticeStateReader>> createReader;
    private readonly Func<IPracticeStateReader, CancellationToken, Task<IObservationSource>> createSource;
    private CancellationTokenSource? active;
    private AppSettings settings;
    private int generation;
    private long adviceUpdatedAt;
    private TimeSpan adviceAge;
    private bool initialized;
    private bool closed;
    private AdviceMessage? activeMessage;

    public DashboardViewModel(SettingsService? settingsService = null, bool loadCatalog = true,
        Func<AppSettings, string, Action<ApiDiagnostic>, IModelClient>? coachingClientFactory = null,
        Func<Action<ApiDiagnostic>, CancellationToken, Task<IPracticeStateReader>>? practiceReaderFactory = null,
        ISpeechService? speechService = null,
        Func<IPracticeStateReader, CancellationToken, Task<IObservationSource>>? observationSourceFactory = null)
    {
        store = settingsService ?? new();
        this.loadCatalog = loadCatalog;
        speech = speechService ?? new SpeechService();
        createModel = coachingClientFactory ?? ((options, key, report) => new FoundryCoachingService(options, key, diagnostics: report));
        createReader = practiceReaderFactory ?? (async (report, token) => await LiveClientDataService.CreateAsync(token, report));
        createSource = observationSourceFactory ?? CreateLiveSourceAsync;
        settings = store.Load();
        selectedChampion = Champions.FirstOrDefault(champion => champion.Id == settings.Profile?.Champion);
        if (settings.Profile?.Answers.Length == 8 && settings.Profile.Answers.All(answer => answer is >= 0 and < 3))
            for (var index = 0; index < 8; index++) Questions[index].SelectedIndex = settings.Profile.Answers[index];
        foreach (var question in Questions)
            question.PropertyChanged += (_, _) => NextQuestionCommand.NotifyCanExecuteChanged();
        selectedVoice = speech.Voices.FirstOrDefault(); voiceEnabled = HasSpanishVoice;
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => Tick(), Application.Current.Dispatcher);
        timer.Start();
    }

    public QuestionViewModel[] Questions { get; } = QuestionnaireService.Questions.Select(question => new QuestionViewModel(question)).ToArray();
    public ChampionChoice[] Champions => ChampionProgressionCatalog.Default.Champions;
    public ObservableCollection<ChampionViewModel> Recommendations { get; } = [];
    public ObservableCollection<AdviceMessage> Messages { get; } = [];
    public ObservableCollection<ApiField> ApiFields { get; } = [];
    public ObservableCollection<PlayerStatsRow> Players { get; } = [];
    public ObservableCollection<ApiDiagnostic> LolDiagnostics { get; } = [];
    public ObservableCollection<ApiDiagnostic> ModelDiagnostics { get; } = [];
    public string[] Voices => speech.Voices;
    public bool HasSpanishVoice => Voices.Length > 0;
    public string VoiceStatus => HasSpanishVoice ? "Voz española disponible" : speech.InitializationError ?? "No hay una voz española compatible instalada.";
    public string LiveRestriction => ProductPolicy.LiveRestriction;
    public string LocalSettingsPath => store.LocalSettingsPath;
    public string ModelName => settings.Deployment;
    public bool CanChangeMode => !IsBusy;
    public string ProfileLabel => SelectedChampion is null ? "Sin perfil" : $"{QuestionnaireService.RoleName(SelectedChampion.Role)} · {SelectedChampion.Name}";
    public QuestionViewModel CurrentQuestion => Questions[QuestionIndex];
    public string QuestionProgress => $"{QuestionIndex + 1} / 8";
    public string NextLabel => QuestionIndex == 7 ? "Ver resultados" : "Siguiente";
    public bool CanGoBack => QuestionIndex > 0 && !ShowResults;
    private PlayerProfile Profile => new(SelectedChampion?.Role ?? PlayerRole.Top, SelectedChampion?.Id ?? "", Questions.Select(question => question.SelectedIndex).ToArray());

    [ObservableProperty] private int questionIndex;
    [ObservableProperty] private bool showResults;
    [ObservableProperty] private string resultSummary = "";
    [ObservableProperty] private ChampionChoice? selectedChampion;
    [ObservableProperty] private string status = "Preparando arranque";
    [ObservableProperty] private string advice = "";
    [ObservableProperty] private string adviceState = "Sin consejo";
    [ObservableProperty] private string ageLabel = "";
    [ObservableProperty] private string stateLabel = "Sin observaciones";
    [ObservableProperty] private string payloadLabel = "0 KB";
    [ObservableProperty] private string latencyLabel = "Sin medición";
    [ObservableProperty] private int requests;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool automaticStartEnabled = true;
    [ObservableProperty] private bool continuousSending = true;
    [ObservableProperty] private bool isWaitingForGame;
    [ObservableProperty] private bool voiceEnabled;
    [ObservableProperty] private string? selectedVoice;
    [ObservableProperty] private int volume = 65;
    [ObservableProperty] private ImageSource? scene;
    [ObservableProperty] private string modelStatus = "Pendiente";
    [ObservableProperty] private string modelReply = "";
    [ObservableProperty] private string apiStatus = "Sin consultar";
    [ObservableProperty] private string apiLastUpdate = "Sin datos";
    [ObservableProperty] private string ownChampion = "Tu campeón";
    [ObservableProperty] private string ownVitals = "Sin datos de partida";
    [ObservableProperty] private string ownLevel = "-";
    [ObservableProperty] private string ownHealth = "- / -";
    [ObservableProperty] private string ownGold = "-";
    [ObservableProperty] private string ownHealthColor = "#52635A";
    [ObservableProperty] private string miniGuide = "";
    [ObservableProperty] private string gamePhase = "Sin fase disponible";
    [ObservableProperty] private string rosterStatus = "Esperando datos de jugadores";

    partial void OnQuestionIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentQuestion)); OnPropertyChanged(nameof(QuestionProgress)); OnPropertyChanged(nameof(NextLabel)); OnPropertyChanged(nameof(CanGoBack));
        NextQuestionCommand.NotifyCanExecuteChanged(); PreviousQuestionCommand.NotifyCanExecuteChanged();
    }
    partial void OnShowResultsChanged(bool value)
    {
        NextQuestionCommand.NotifyCanExecuteChanged(); PreviousQuestionCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanGoBack));
    }
    partial void OnSelectedChampionChanged(ChampionChoice? value) => OnPropertyChanged(nameof(ProfileLabel));
    partial void OnVoiceEnabledChanged(bool value) { if (!value) speech.Cancel(); }
    partial void OnAutomaticStartEnabledChanged(bool value)
    {
        if (!value) active?.Cancel();
        else if (initialized && !closed && !IsBusy) StartCommand.Execute(null);
    }
    partial void OnIsBusyChanged(bool value) { StartCommand.NotifyCanExecuteChanged(); OnPropertyChanged(nameof(CanChangeMode)); }
    private bool CanNextQuestion() => !ShowResults && CurrentQuestion.SelectedIndex >= 0;

    [RelayCommand(CanExecute = nameof(CanNextQuestion))]
    private async Task NextQuestionAsync()
    {
        if (QuestionIndex < 7) { QuestionIndex++; return; }
        var result = QuestionnaireService.Evaluate(Questions.Select(question => question.SelectedIndex).ToArray());
        ResultSummary = $"{QuestionnaireService.RoleName(result.Roles[0])}: {result.Reasons[0]}. Otra opción: {QuestionnaireService.RoleName(result.Roles[1])}.";
        Recommendations.Clear();
        foreach (var champion in result.Champions) Recommendations.Add(new(champion));
        SelectedChampion = result.Champions[0]; ShowResults = true;
        if (!loadCatalog) return;
        try
        {
            await catalog.InitializeAsync(lifetime.Token);
            foreach (var champion in Recommendations.ToArray()) champion.Portrait = await catalog.GetPortraitAsync(champion.Choice.Id, lifetime.Token);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.IO.IOException or InvalidOperationException or System.Text.Json.JsonException or NotSupportedException)
        { if (!closed) Status = "Retratos no disponibles; el resultado sigue siendo válido."; }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))] private void PreviousQuestion() => QuestionIndex--;
    [RelayCommand] private void RepeatQuiz()
    {
        ShowResults = false; QuestionIndex = 0;
        foreach (var question in Questions) question.SelectedIndex = -1;
    }
    [RelayCommand] private void ChooseChampion(ChampionViewModel champion) => SelectedChampion = champion.Choice;
    [RelayCommand] private void SaveProfile()
    {
        if (SelectedChampion is null) return;
        try { settings = store.Load() with { Profile = Profile }; store.Save(settings); ProfileSaved?.Invoke(this, EventArgs.Empty); }
        catch (Exception exception) when (IsExpected(exception)) { Status = "No se pudo guardar el perfil."; }
    }

    public event EventHandler? ProfileSaved;

    internal void AppendMessage(AdviceMessage message)
    {
        Messages.Add(message);
        while (Messages.Count > MaxMessages) Messages.RemoveAt(0);
    }

    public IReadOnlyDictionary<string, PanelLayout> LoadPanelLayout() => store.LoadLayout();

    public void SavePanelLayout(IReadOnlyDictionary<string, PanelLayout> layout)
    {
        try { store.SaveLayout(layout); }
        catch (Exception exception) when (exception is System.IO.IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { Status = "No se pudo guardar la disposición de las ventanas."; }
    }

    public async Task InitializeAsync()
    {
        if (initialized || closed) return;
        initialized = true;
        if (AutomaticStartEnabled) await StartCommand.ExecuteAsync(null);
    }

    private bool CanStart() => !IsBusy && !closed;
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (!CanStart()) return;
        IsBusy = true; AutomaticStartEnabled = true;
        Requests = 0; ClearAdvice(); Scene = null;
        ApiStatus = "Sin consultar"; ApiLastUpdate = "Sin datos"; ApiFields.Clear(); Players.Clear();
        OwnChampion = "Tu campeón"; OwnVitals = "Sin datos de partida"; RosterStatus = "Esperando datos de jugadores";
        OwnLevel = "-"; OwnHealth = "- / -"; OwnGold = "-"; OwnHealthColor = "#52635A";
        MiniGuide = ""; GamePhase = "Sin fase disponible";
        ModelStatus = "Comprobando"; ModelReply = "";
        active = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var sessionGeneration = ++generation;
        try
        {
            settings = store.Load(); SettingsService.ValidateEndpoint(settings.Endpoint); OnPropertyChanged(nameof(ModelName));
            if (string.IsNullOrWhiteSpace(settings.Deployment)) throw new ArgumentException("Falta Model.");
            var client = createModel(settings, store.LoadKey() ?? throw new ArgumentException("Falta ApiKey."), ReportDiagnostic);
            using var clientLifetime = client as IDisposable;
            var progress = new UiProgress<SessionUpdate>(update =>
            {
                if (closed || sessionGeneration != generation) return;
                Status = update.Status;
                if (update.WaitingForGame is { } waiting) IsWaitingForGame = waiting;
                if (update.ModelReply is { } reply) { ModelReply = reply; ModelStatus = "Disponible"; }
                if (update.MiniGuide is { } guide) MiniGuide = guide;
                if (update.Snapshot is { } snapshot) ApplySnapshot(snapshot, update.Champion);
                Requests = Math.Max(Requests, update.Requests);
                if (update.Observation is { } observation)
                {
                    Scene = ImageCodec.Decode(observation.Jpeg); PayloadLabel = $"{observation.Jpeg.Length / 1024.0:0.0} KB";
                    ApplySnapshot(observation.State, observation.Practice?.Champion);
                }
                if (update.ClearAdvice) ClearAdvice();
                else if (update.Advice is { } result)
                {
                    if (activeMessage is null)
                    {
                        foreach (var message in Messages) message.IsLatest = false;
                        activeMessage = new AdviceMessage { IsLatest = true };
                        AppendMessage(activeMessage);
                    }
                    activeMessage.Text = result.Text;
                    activeMessage.State = result.IsFinal ? "Completo" : "Borrador";
                    Advice = result.Text; AdviceState = result.IsFinal ? "Texto completo" : "Borrador";
                    adviceAge = result.Age; adviceUpdatedAt = TimeProvider.System.GetTimestamp();
                    if (result.IsFinal)
                    {
                        activeMessage = null;
                        LatencyLabel = $"{result.Age.TotalSeconds:0.0} s";
                        if (VoiceEnabled) speech.Speak(result.Text, SelectedVoice, Volume);
                    }
                }
            });
            var recentAdvice = Messages.Where(message => message.State == "Completo")
                .Select(message => message.Text).TakeLast(5).ToArray();
            await new AutomaticCoachingWorkflow(client, token => createReader(ReportDiagnostic, token), createSource,
                waitForSpeech: WaitForSpeechAsync)
                .RunAsync(Profile, ContinuousSending ? null : settings.RequestLimit, progress, active.Token, recentAdvice);
        }
        catch (Exception exception) when (IsExpected(exception))
        {
            generation++; ClearAdvice(); Scene = null; Status = DescribeError(exception);
            if (ModelStatus != "Disponible") ModelStatus = Status;
            ApiStatus = "Sin conexión activa";
        }
        finally { IsWaitingForGame = false; AutomaticStartEnabled = false; capture.Dispose(); active?.Dispose(); active = null; IsBusy = false; }
    }

    private async Task<IObservationSource> CreateLiveSourceAsync(IPracticeStateReader reader, CancellationToken cancellationToken)
    {
        CaptureWindow? gameWindow;
        while ((gameWindow = WindowCaptureService.FindGameWindow()) is null)
        {
            Status = "Esperando la ventana del juego"; IsWaitingForGame = true;
            await Task.Delay(PracticeStartDetector.PollInterval, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested(); capture.Start(gameWindow); IsWaitingForGame = false;
        return new LiveObservationSource(capture, reader);
    }

    private Task WaitForSpeechAsync(CancellationToken cancellationToken)
    {
        if (!VoiceEnabled) return Task.CompletedTask;
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess()) return speech.WaitUntilFinishedAsync(cancellationToken);
        return dispatcher.InvokeAsync(() => speech.WaitUntilFinishedAsync(cancellationToken),
            DispatcherPriority.Normal, cancellationToken).Task.Unwrap();
    }

    private void ApplySnapshot(GameSnapshot snapshot, string? champion)
    {
        ApiStatus = "Partida disponible"; ApiLastUpdate = DateTime.Now.ToString("HH:mm:ss");
        StateLabel = $"Nivel {snapshot.Level} · Vida {snapshot.Health:0}/{snapshot.MaxHealth:0} · Oro {snapshot.Gold:0}";
        OwnChampion = champion ?? snapshot.Roster.FirstOrDefault(player => player.IsOwn)?.Champion ?? "Tu campeón";
        OwnVitals = $"Nv. {snapshot.Level?.ToString() ?? "-"} · Vida {snapshot.Health?.ToString("0") ?? "-"}/{snapshot.MaxHealth?.ToString("0") ?? "-"} · Oro {snapshot.Gold?.ToString("0") ?? "-"}";
        OwnLevel = snapshot.Level?.ToString() ?? "-";
        OwnHealth = $"{snapshot.Health?.ToString("0") ?? "-"} / {snapshot.MaxHealth?.ToString("0") ?? "-"}";
        OwnGold = snapshot.Gold?.ToString("0") ?? "-";
        OwnHealthColor = HealthColor(snapshot.Health, snapshot.MaxHealth);
        GamePhase = $"Fase estimada: {MatchTacticalContext.EstimatePhase(snapshot).Phase}";
        Players.Clear();
        foreach (var player in snapshot.Roster.OrderBy(player => player.Relation == "Rival" ? 0 : player.IsOwn ? 1 : 2))
            Players.Add(new(player));
        RosterStatus = Players.Count == 0 ? "Esperando datos de jugadores" : $"{Players.Count} campeones · K/D/A y súbditos (CS)";
        ApiFields.Clear();
        ApiFields.Add(new("Campeón", OwnChampion)); ApiFields.Add(new("Nivel", OwnLevel, "#145DA0"));
        ApiFields.Add(new("Vida", OwnHealth, OwnHealthColor));
        ApiFields.Add(new("Vida restante", snapshot.Health is not null && snapshot.MaxHealth is > 0 ? $"{snapshot.Health / snapshot.MaxHealth * 100:0}%" : "-", OwnHealthColor));
        ApiFields.Add(new("Oro disponible", OwnGold, "#8A6514")); ApiFields.Add(new("Tiempo de partida", $"{snapshot.GameTime:0} s"));
        ApiFields.Add(new("Rivales", "Bots verificados"));
        if (snapshot.Player is { } details)
        {
            ApiFields.Add(new("K/D/A", $"{details.Kills?.ToString() ?? "-"}/{details.Deaths?.ToString() ?? "-"}/{details.Assists?.ToString() ?? "-"}"));
            ApiFields.Add(new("Súbditos (CS)", details.CreepScore?.ToString() ?? "-"));
            ApiFields.Add(new("Visión", details.WardScore?.ToString("0.#") ?? "-"));
            ApiFields.Add(new(details.ResourceType ?? "Recurso", $"{details.Resource?.ToString("0") ?? "-"} / {details.MaxResource?.ToString("0") ?? "-"}"));
            foreach (var (key, name) in new[] { ("attackDamage", "Daño de ataque"), ("abilityPower", "Poder de habilidad"),
                ("armor", "Armadura"), ("magicResist", "Resistencia mágica"), ("attackSpeed", "Velocidad de ataque"),
                ("abilityHaste", "Aceleración"), ("moveSpeed", "Velocidad") })
                ApiFields.Add(new(name, details.CombatStats.TryGetValue(key, out var stat) ? stat.ToString("0.##") : "-"));
            ApiFields.Add(new("Habilidades", string.Join(" · ", details.AbilityLevels.Select(ability => $"{ability.Key}: {ability.Value}"))));
            ApiFields.Add(new("Hechizos", string.Join(", ", details.SummonerSpells)));
            ApiFields.Add(new("Runas", string.Join(", ", details.Runes)));
        }
    }

    private static string HealthColor(double? health, double? maxHealth)
        => health is not { } current || maxHealth is not > 0 ? "#52635A"
            : current / maxHealth < 0.3 ? "#AC4539" : current / maxHealth < 0.5 ? "#8A6514" : "#205E4F";

    private void ReportDiagnostic(ApiDiagnostic entry)
    {
        void Add()
        {
            if (closed) return;
            var collection = entry.Api == "LoL" ? LolDiagnostics : ModelDiagnostics;
            collection.Add(entry); while (collection.Count > 200) collection.RemoveAt(0);
        }
        var dispatcher = Application.Current.Dispatcher;
        if (dispatcher.CheckAccess()) Add(); else _ = dispatcher.BeginInvoke(new Action(Add));
    }
    private sealed class UiProgress<T>(Action<T> report) : IProgress<T>
    {
        private readonly Dispatcher dispatcher = Application.Current.Dispatcher;
        public void Report(T value)
        {
            if (dispatcher.CheckAccess()) report(value);
            else _ = dispatcher.BeginInvoke(new Action(() => report(value)));
        }
    }
    [RelayCommand] private void ClearDiagnostics() { LolDiagnostics.Clear(); ModelDiagnostics.Clear(); }
    [RelayCommand] private void Stop()
    {
        AutomaticStartEnabled = false; active?.Cancel(); generation++; ClearAdvice(); Scene = null;
        Status = "Detenido"; ApiStatus = "Sin conexión activa";
    }
    private void ClearAdvice()
    {
        if (activeMessage is not null) activeMessage.State = "Incompleto · no vigente";
        activeMessage = null;
        Advice = ""; AdviceState = "Sin consejo"; AgeLabel = ""; adviceUpdatedAt = 0; speech.Cancel();
    }
    private void Tick()
    {
        if (adviceUpdatedAt == 0) return;
        var age = adviceAge + TimeProvider.System.GetElapsedTime(adviceUpdatedAt); AgeLabel = $"{age.TotalSeconds:0.0} s desde la captura";
        if (age >= CoachingSession.MaximumAge) ClearAdvice();
    }
    private static bool IsExpected(Exception exception) => exception is ArgumentException or InvalidOperationException or HttpRequestException
        or ClientResultException or OperationCanceledException or System.IO.IOException or CryptographicException or UnauthorizedAccessException
        or System.Text.Json.JsonException or System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception;
    private static string DescribeError(Exception exception) => exception switch
    {
        ModelOutputException output => output.Message,
        System.Text.Json.JsonException => "Formato inválido en localsettings.json: Profile debe ser un objeto, null o vacío; RequestLimit debe ser un número.",
        CryptographicException => "La ApiKey cifrada de localsettings.json no puede leerse con este usuario de Windows.",
        PracticeUnavailableException practice => practice.Message,
        OperationCanceledException => "Operación detenida o tiempo de espera agotado.",
        ClientResultException { Status: 401 or 403 } => "Azure rechazó la credencial o sus permisos.",
        ClientResultException { Status: 404 } => "Revisa Endpoint y Model en localsettings.json.",
        ClientResultException { Status: 429 } => "Azure limita las solicitudes. Sesión detenida; espera antes de reintentar.",
        ClientResultException { Status: 400 } => "El modelo rechazó la solicitud. Comprueba visión y parámetros.",
        ArgumentException => "Revisa Endpoint, Model y ApiKey en localsettings.json.",
        _ => "No se pudo completar la operación. Revisa la conexión y localsettings.json."
    };
    public async Task ShutdownAsync()
    {
        closed = true; Stop(); lifetime.Cancel();
        if (StartCommand.ExecutionTask is { } operation) try { await operation; } catch (Exception exception) when (IsExpected(exception)) { }
        Dispose();
    }
    public void Dispose() { timer.Stop(); capture.Dispose(); speech.Dispose(); catalog.Dispose(); lifetime.Dispose(); }
}