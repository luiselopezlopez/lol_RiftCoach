using System.Net.Http;
using System.Text;
using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed record SessionUpdate(string Status, AdviceUpdate? Advice = null, Observation? Observation = null,
    int Requests = 0, bool ClearAdvice = false, string? ModelReply = null, GameSnapshot? Snapshot = null,
    string? Champion = null, bool? WaitingForGame = null, string? MiniGuide = null, string? MatchRecap = null,
    double? MatchRecapGameTime = null, string? VoiceQuestionStatus = null, int? ImmediateActionsUntilTactical = null);

public sealed class CoachingSession(IObservationSource source, ICoachingClient client, TimeProvider? timeProvider = null,
    IEnumerable<string>? recentAdvice = null, Func<CancellationToken, Task>? waitForSpeech = null,
    Func<string?>? takeSpokenQuestion = null)
{
    private const int ImmediateActionsPerTacticalAdvice = 3;
    private const int EndConfirmationAttempts = 4;
    private static readonly TimeSpan EndConfirmationDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MatchRecapTimeout = TimeSpan.FromSeconds(90);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Queue<string> adviceHistory = new((recentAdvice ?? Array.Empty<string>())
        .Where(AdviceValidator.IsValidForHistory).TakeLast(MaxHistory));
    public const int MaxHistory = 30;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(10);

    public async Task RunAsync(PlayerProfile profile, IProgress<SessionUpdate> progress, CancellationToken cancellationToken,
        CancellationToken matchEndToken = default)
    {
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requests = 0;
        var matchDataEnded = false;
        var matchEndedManually = false;
        var recapAvailable = false;
        var immediateActionsSinceTacticalAdvice = 0;
        Observation? latestObservation = null;
        MatchRecapRecorder? recapRecorder = null;
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                if (matchEndToken.IsCancellationRequested)
                {
                    matchEndedManually = true;
                    break;
                }
                using var iteration = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, matchEndToken);
                try
                {
                    if (requests > 0 && waitForSpeech is not null)
                        await waitForSpeech(iteration.Token);
                    var observation = await CaptureUntilUnavailableAsync(latestObservation, progress, requests, iteration.Token);
                    if (observation is null)
                    {
                        matchDataEnded = true;
                        recapRecorder!.ForceLatestSample();
                        recapAvailable = await CreateMatchRecapAsync(recapRecorder, progress, requests,
                            MatchRecapEndEvidence.LiveClientDataUnavailableAfterRetries, lifetime.Token);
                        break;
                    }
                    ProductPolicy.EnsureAllowed(observation);
                    recapRecorder ??= new MatchRecapRecorder(observation.Practice?.Champion ?? profile.Champion,
                        QuestionnaireService.RoleName(profile.Role));
                    recapRecorder.Observe(observation);
                    latestObservation = observation;
                    progress.Report(new("Escena preparada", Observation: observation));
                    var age = clock.GetElapsedTime(observation.CapturedAt);
                    if (age >= MaximumAge) throw new PracticeUnavailableException("La observación ha caducado antes del envío.");
                    var requestMode = immediateActionsSinceTacticalAdvice >= ImmediateActionsPerTacticalAdvice
                        ? CoachingRequestMode.TacticalAdvice : CoachingRequestMode.ImmediateAction;
                    observation = observation with { CoachingRequestMode = requestMode };
                    var spokenQuestion = takeSpokenQuestion?.Invoke();
                    if (!string.IsNullOrWhiteSpace(spokenQuestion))
                        observation = observation with { SpokenQuestion = spokenQuestion };
                    requests++;
                    progress.Report(new("Analizando", Requests: requests,
                        ImmediateActionsUntilTactical: ImmediateActionsPerTacticalAdvice - immediateActionsSinceTacticalAdvice));
                    using var expiry = new CancellationTokenSource(MaximumAge - age, clock);
                    using var request = CancellationTokenSource.CreateLinkedTokenSource(iteration.Token, expiry.Token);
                    var text = new StringBuilder();
                    try
                    {
                        await foreach (var fragment in client.StreamAsync(observation, profile, adviceHistory.ToArray(), request.Token))
                        {
                            request.Token.ThrowIfCancellationRequested();
                            if (clock.GetElapsedTime(observation.CapturedAt) >= MaximumAge) break;
                            text.Append(fragment);
                            if (text.Length > 600) throw new InvalidOperationException("Respuesta demasiado larga; sesión detenida.");
                            progress.Report(new("Borrador", new(text.ToString(), false,
                                clock.GetElapsedTime(observation.CapturedAt), requestMode), Requests: requests));
                        }
                        request.Token.ThrowIfCancellationRequested();
                        age = clock.GetElapsedTime(observation.CapturedAt);
                        var finalText = text.ToString().Trim();
                        if (age < MaximumAge && AdviceValidator.IsValid(finalText, requestMode))
                        {
                            immediateActionsSinceTacticalAdvice = requestMode == CoachingRequestMode.ImmediateAction
                                ? immediateActionsSinceTacticalAdvice + 1 : 0;
                            progress.Report(new("Consejo disponible", new(finalText, true, age, requestMode), Requests: requests,
                                ImmediateActionsUntilTactical: ImmediateActionsPerTacticalAdvice - immediateActionsSinceTacticalAdvice));
                            adviceHistory.Enqueue(finalText);
                            recapRecorder.RecordAdvice(finalText);
                            while (adviceHistory.Count > MaxHistory) adviceHistory.Dequeue();
                        }
                        else
                            progress.Report(new("Respuesta descartada", Requests: requests, ClearAdvice: true));
                    }
                    catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                    {
                        if (matchEndToken.IsCancellationRequested)
                        {
                            matchEndedManually = true;
                            break;
                        }
                        progress.Report(new("Respuesta caducada", Requests: requests, ClearAdvice: true));
                    }
                    catch (System.ClientModel.ClientResultException exception)
                        when (!lifetime.IsCancellationRequested && IsContentFilterRejection(exception))
                    {
                        var status = spokenQuestion is not null
                            ? "Azure bloqueó la pregunta por el filtro de contenido. La sesión continúa."
                            : "Azure bloqueó este prompt por el filtro de contenido. La sesión continúa.";
                        var voiceStatus = spokenQuestion is null ? null
                            : "Azure bloqueó la pregunta por contenido. Reformúlala para volver a intentarlo.";
                        progress.Report(new(status, Requests: requests, VoiceQuestionStatus: voiceStatus));
                    }
                }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested && matchEndToken.IsCancellationRequested)
                {
                    matchEndedManually = true;
                    break;
                }
                await Task.Yield();
            }
            lifetime.Token.ThrowIfCancellationRequested();
            if (matchEndedManually)
            {
                if (recapRecorder is null)
                    progress.Report(new("Recap de partida no disponible", Requests: requests));
                else
                {
                    recapRecorder.ForceLatestSample();
                    recapAvailable = await CreateMatchRecapAsync(recapRecorder, progress, requests,
                        MatchRecapEndEvidence.PlayerIndicatedMatchEnded, lifetime.Token);
                }
            }
        }
        finally
        {
            var stopped = lifetime.IsCancellationRequested;
            await lifetime.CancelAsync();
            var status = stopped ? "Sesión detenida"
                : matchDataEnded || matchEndedManually ? recapAvailable ? "Recap de partida disponible" : "Recap de partida no disponible"
                : "Sesión detenida";
            progress.Report(new(status, Requests: requests, ClearAdvice: true));
        }
    }

    private static bool IsContentFilterRejection(System.ClientModel.ClientResultException exception)
        => exception.Message.Contains("content_filter", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("content management policy", StringComparison.OrdinalIgnoreCase);

    private async Task<Observation?> CaptureUntilUnavailableAsync(Observation? latestObservation,
        IProgress<SessionUpdate> progress, int requests, CancellationToken cancellationToken)
    {
        if (latestObservation is null)
            return await source.CaptureAsync(cancellationToken);

        for (var attempt = 1; attempt <= EndConfirmationAttempts; attempt++)
        {
            try { return await source.CaptureAsync(cancellationToken); }
            catch (HttpRequestException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == EndConfirmationAttempts) return null;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == EndConfirmationAttempts) return null;
            }
            progress.Report(new("Reintentando la API local", Requests: requests));
            await Task.Delay(EndConfirmationDelay, clock, cancellationToken);
        }
        return null;
    }

    private async Task<bool> CreateMatchRecapAsync(MatchRecapRecorder recorder,
        IProgress<SessionUpdate> progress, int requests, MatchRecapEndEvidence endEvidence,
        CancellationToken cancellationToken)
    {
        if (client is not IMatchRecapClient recapClient)
        {
            progress.Report(new("Recap de partida no disponible", Requests: requests));
            return false;
        }

        progress.Report(new("Generando recap de la partida", Requests: requests));
        using var timeout = new CancellationTokenSource(MatchRecapTimeout, clock);
        using var recapLifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            var recap = await recapClient.CreateMatchRecapAsync(
                recorder.Build(endEvidence), recapLifetime.Token);
            recapLifetime.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(recap)) throw new InvalidOperationException("El modelo no devolvió un recap.");
            progress.Report(new("Recap de partida disponible", Requests: requests, MatchRecap: recap,
                MatchRecapGameTime: recorder.LastGameTime));
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or System.Text.Json.JsonException
            or InvalidOperationException or System.ClientModel.ClientResultException or OperationCanceledException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(new("Recap de partida no disponible", Requests: requests));
            return false;
        }
    }

}
