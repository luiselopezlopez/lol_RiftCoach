using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed class AutomaticCoachingWorkflow(IModelClient model,
    Func<CancellationToken, Task<IPracticeStateReader>> createReader,
    Func<IPracticeStateReader, CancellationToken, Task<IObservationSource>> createSource,
    TimeProvider? timeProvider = null,
    Func<CancellationToken, Task>? waitForSpeech = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task RunAsync(PlayerProfile profile, int? requestLimit, IProgress<SessionUpdate> progress,
        CancellationToken cancellationToken, IReadOnlyList<string>? recentAdvice = null)
    {
        progress.Report(new("Comprobando el modelo", WaitingForGame: false));
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock))
        using (var probe = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken))
        {
            var reply = await model.ProbeAsync(probe.Token);
            probe.Token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(reply)) throw new InvalidOperationException("El modelo no respondió a la pregunta inicial.");
            progress.Report(new("Modelo disponible", ModelReply: reply[..Math.Min(reply.Length, 400)]));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var reader = await createReader(cancellationToken);
        using var readerLifetime = reader as IDisposable;
        progress.Report(new("Esperando el inicio de la partida", WaitingForGame: true));
        var detection = new ForwardProgress<string>(status => progress.Report(new(status)));
        var state = await new PracticeStartDetector(reader, clock).WaitAsync(detection, cancellationToken);
        var detectedStatus = state.Verification.BotVerificationBypassed
            ? "Partida detectada; verificación de bots omitida (depuración)"
            : "Partida detectada";
        progress.Report(new(detectedStatus, Snapshot: state.Snapshot, Champion: state.Verification.Champion, WaitingForGame: false));
        if (model is IChampionGuideClient guideClient)
        {
            progress.Report(new("Preparando miniguía del campeón"));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30), clock);
            using var guide = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
            try
            {
                var text = await guideClient.CreateGuideAsync(state, profile, guide.Token);
                guide.Token.ThrowIfCancellationRequested();
                progress.Report(new("Miniguía disponible", MiniGuide: text));
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.Net.Http.HttpRequestException
                or System.Text.Json.JsonException or OperationCanceledException or System.ClientModel.ClientResultException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report(new("Miniguía no disponible; continúa el entrenamiento", MiniGuide: "No se pudo preparar la miniguía del campeón."));
            }
        }
        var source = await createSource(reader, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await new CoachingSession(source, model, clock, recentAdvice, waitForSpeech)
            .RunAsync(profile, requestLimit, progress, cancellationToken);
    }

    private sealed class ForwardProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}