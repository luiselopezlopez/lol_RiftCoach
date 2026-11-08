using System.Runtime.CompilerServices;
using System.Text.Json;
using LoLCoach.Models;
using LoLCoach.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace LoLCoach.Tests;

public class AutomaticWorkflowTests
{
    [Fact]
    public async Task ModelMustAnswerBeforePollingAndOnlyThenCanStreamingStart()
    {
        var clock = new FakeTimeProvider();
        var model = new Model();
        var readerCalls = 0;
        var captureCalls = 0;
        var reader = new Reader(clock);
        using var stop = new CancellationTokenSource();
        var workflow = new AutomaticCoachingWorkflow(model,
            _ => { readerCalls++; return Task.FromResult<IPracticeStateReader>(reader); },
            (_, _) => { captureCalls++; return Task.FromResult<IObservationSource>(new Source(clock)); }, clock);
        var progress = new Collector();
        var running = workflow.RunAsync(new(PlayerRole.Top, "", []), progress, stop.Token);
        Assert.Equal(0, readerCalls);
        Assert.Equal(0, captureCalls);
        Assert.Equal(0, model.StreamCalls);
        model.Reply.SetResult("Disponible.");
        await progress.FirstAdvice.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, readerCalls);
        Assert.Equal(1, captureCalls);
        Assert.Equal(1, model.StreamCalls);
        Assert.Contains(progress.Updates, update => update.ModelReply == "Disponible.");
        Assert.Contains(progress.Updates, update => update.Snapshot?.Level == 3 && update.Champion == "Garen");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GuideIsPreparedOnceBeforeOrdersAndFailureDoesNotStopTraining(bool fails)
    {
        var clock = new FakeTimeProvider();
        var model = new GuideModel(fails);
        var progress = new Collector();
        using var stop = new CancellationTokenSource();
        var running = new AutomaticCoachingWorkflow(model, _ => Task.FromResult<IPracticeStateReader>(new Reader(clock)),
            (_, _) => Task.FromResult<IObservationSource>(new Source(clock)), clock)
            .RunAsync(new(PlayerRole.Top, "Garen", []), progress, stop.Token);
        await progress.FirstAdvice.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, model.GuideCalls);
        Assert.Equal(1, model.StreamCalls);
        var guideUpdate = Assert.Single(progress.Updates, update => update.MiniGuide is not null);
        Assert.Contains(fails ? "No se pudo" : "Garen", guideUpdate.MiniGuide!);
        Assert.True(progress.Updates.IndexOf(guideUpdate) < progress.Updates.FindIndex(update => update.Observation is not null));
    }

    [Fact]
    public async Task CancellingGuidePreventsCaptureAndOrders()
    {
        var clock = new FakeTimeProvider();
        var model = new GuideModel(false, new(TaskCreationOptions.RunContinuationsAsynchronously));
        using var stop = new CancellationTokenSource();
        var sourceCalls = 0;
        var running = new AutomaticCoachingWorkflow(model, _ => Task.FromResult<IPracticeStateReader>(new Reader(clock)),
            (_, _) => { sourceCalls++; return Task.FromResult<IObservationSource>(new Source(clock)); }, clock)
            .RunAsync(new(PlayerRole.Top, "Garen", []), new Collector(), stop.Token);
        await model.GuideStarted.Task;
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.Equal(0, sourceCalls);
        Assert.Equal(0, model.StreamCalls);
    }

    [Fact]
    public async Task GuideTimeoutDoesNotBlockFreshObservation()
    {
        var clock = new FakeTimeProvider();
        var model = new GuideModel(false, new(TaskCreationOptions.RunContinuationsAsynchronously));
        var progress = new Collector();
        using var stop = new CancellationTokenSource();
        var running = new AutomaticCoachingWorkflow(model, _ => Task.FromResult<IPracticeStateReader>(new Reader(clock)),
            (_, _) => Task.FromResult<IObservationSource>(new Source(clock)), clock)
            .RunAsync(new(PlayerRole.Top, "Garen", []), progress, stop.Token);
        await model.GuideStarted.Task;
        clock.Advance(TimeSpan.FromSeconds(31));
        await progress.FirstAdvice.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, model.StreamCalls);
        Assert.Contains(progress.Updates, update => update.MiniGuide?.StartsWith("No se pudo") == true);
    }

    private sealed class GuideModel(bool fails, TaskCompletionSource<string>? reply = null) : IModelClient, IChampionGuideClient
    {
        public TaskCompletionSource GuideStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int GuideCalls { get; private set; }
        public int StreamCalls { get; private set; }
        public Task<string> ProbeAsync(CancellationToken cancellationToken) => Task.FromResult("Disponible.");
        public Task<string> CreateGuideAsync(PracticeState state, PlayerProfile profile, CancellationToken cancellationToken)
        {
            GuideCalls++;
            GuideStarted.TrySetResult();
            Assert.Equal("Garen", state.Verification.Champion);
            if (reply is not null) return reply.Task.WaitAsync(cancellationToken);
            return fails ? Task.FromException<string>(new System.Net.Http.HttpRequestException("Unavailable")) : Task.FromResult("Garen: usa Q para acercarte.");
        }
        public async IAsyncEnumerable<string> StreamAsync(Observation observation, PlayerProfile profile,
            IReadOnlyList<string> recentAdvice, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Assert.Equal(1, GuideCalls); StreamCalls++;
            await Task.CompletedTask; yield return "Espera";
        }
    }

    [Fact]
    public async Task FailedModelCheckNeverTouchesLolOrStartsCapture()
    {
        var model = new Model();
        model.Reply.SetException(new InvalidOperationException("Model unavailable."));
        var workflow = new AutomaticCoachingWorkflow(model,
            _ => throw new Xunit.Sdk.XunitException("LoL must not be queried."),
            (_, _) => throw new Xunit.Sdk.XunitException("Capture must not start."));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.RunAsync(new(PlayerRole.Top, "", []), new Collector(), default));
    }

    private sealed class Collector : IProgress<SessionUpdate>
    {
        public List<SessionUpdate> Updates { get; } = [];
        public TaskCompletionSource FirstAdvice { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Report(SessionUpdate value)
        {
            Updates.Add(value);
            if (value.Advice?.IsFinal == true) FirstAdvice.TrySetResult();
        }
    }

    private sealed class Model : IModelClient
    {
        public TaskCompletionSource<string> Reply { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StreamCalls { get; private set; }
        public Task<string> ProbeAsync(CancellationToken cancellationToken) => Reply.Task.WaitAsync(cancellationToken);
        public async IAsyncEnumerable<string> StreamAsync(Observation observation, PlayerProfile profile,
            IReadOnlyList<string> recentAdvice, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            StreamCalls++;
            await Task.CompletedTask;
            yield return "Avanza.";
        }
    }

    private sealed class Reader(TimeProvider clock) : IPracticeStateReader
    {
        public Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken)
        {
            using var active = JsonDocument.Parse("""{"riotId":"local#test"}""");
            using var players = JsonDocument.Parse("""[{"riotId":"local#test","team":"ORDER","isBot":false,"championName":"Garen"},{"team":"CHAOS","isBot":true}]""");
            return Task.FromResult(new PracticeState(new(120, 3, 500, 800, 600),
                BotMatchVerifier.Verify(active.RootElement, players.RootElement), clock.GetTimestamp()));
        }
    }

    private sealed class Source(TimeProvider clock) : IObservationSource
    {
        private bool sampled;
        public async Task<Observation> CaptureAsync(CancellationToken cancellationToken)
        {
            if (sampled) await Task.Delay(Timeout.Infinite, cancellationToken);
            sampled = true;
            return TestObservations.Create(new(120, 3, 500, 800, 600), clock.GetTimestamp());
        }
    }
}
