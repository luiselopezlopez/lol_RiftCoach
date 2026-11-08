using System.Text;
using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed record SessionUpdate(string Status, AdviceUpdate? Advice = null, Observation? Observation = null,
    int Requests = 0, bool ClearAdvice = false, string? ModelReply = null, GameSnapshot? Snapshot = null,
    string? Champion = null, bool? WaitingForGame = null, string? MiniGuide = null);

public sealed class CoachingSession(IObservationSource source, ICoachingClient client, TimeProvider? timeProvider = null,
    IEnumerable<string>? recentAdvice = null, Func<CancellationToken, Task>? waitForSpeech = null,
    Func<string?>? takeSpokenQuestion = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly Queue<string> adviceHistory = new((recentAdvice ?? Array.Empty<string>())
        .Where(AdviceValidator.IsValid).TakeLast(MaxHistory));
    public const int MaxHistory = 30;
    public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(10);

    public async Task RunAsync(PlayerProfile profile, int? requestLimit, IProgress<SessionUpdate> progress,
        CancellationToken cancellationToken)
    {
        if (requestLimit is < 1 or > 100000) throw new ArgumentOutOfRangeException(nameof(requestLimit));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var requests = 0;
        var reachedLimit = false;
        try
        {
            while (!lifetime.IsCancellationRequested)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                if (requests > 0 && waitForSpeech is not null)
                    await waitForSpeech(lifetime.Token);
                var observation = await source.CaptureAsync(lifetime.Token);
                ProductPolicy.EnsureAllowed(observation);
                progress.Report(new("Escena preparada", Observation: observation));
                var age = clock.GetElapsedTime(observation.CapturedAt);
                if (age >= MaximumAge) throw new PracticeUnavailableException("La observación ha caducado antes del envío.");
                var spokenQuestion = takeSpokenQuestion?.Invoke();
                if (!string.IsNullOrWhiteSpace(spokenQuestion))
                    observation = observation with { SpokenQuestion = spokenQuestion };
                requests++;
                progress.Report(new("Analizando", Requests: requests));
                using var expiry = new CancellationTokenSource(MaximumAge - age, clock);
                using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, expiry.Token);
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
                            clock.GetElapsedTime(observation.CapturedAt)), Requests: requests));
                    }
                    request.Token.ThrowIfCancellationRequested();
                    age = clock.GetElapsedTime(observation.CapturedAt);
                    var finalText = text.ToString().Trim();
                    if (age < MaximumAge && AdviceValidator.IsValid(finalText))
                    {
                        progress.Report(new("Consejo disponible", new(finalText, true, age), Requests: requests));
                        adviceHistory.Enqueue(finalText);
                        while (adviceHistory.Count > MaxHistory) adviceHistory.Dequeue();
                    }
                    else
                        progress.Report(new("Respuesta descartada", Requests: requests, ClearAdvice: true));
                }
                catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
                {
                    progress.Report(new("Respuesta caducada", Requests: requests, ClearAdvice: true));
                }
                if (requestLimit is { } limit && requests >= limit) { reachedLimit = true; break; }
                await Task.Yield();
            }
            lifetime.Token.ThrowIfCancellationRequested();
        }
        finally
        {
            var stopped = lifetime.IsCancellationRequested;
            await lifetime.CancelAsync();
            progress.Report(new(reachedLimit ? "Límite de solicitudes alcanzado" : "Sesión detenida",
                Requests: requests, ClearAdvice: !reachedLimit || stopped));
        }
    }
}
