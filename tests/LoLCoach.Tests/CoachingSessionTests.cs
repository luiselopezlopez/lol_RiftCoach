using System.Runtime.CompilerServices;
using System.Threading.Channels;
using LoLCoach.Models;
using LoLCoach.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace LoLCoach.Tests;

public class CoachingSessionTests
{
    private static readonly PlayerProfile Profile = new(PlayerRole.Top, "Garen", []);

    [Fact]
    public async Task RequestCapStopsSession()
    {
        var clock = new FakeTimeProvider();
        var progress = new Collector();
        var client = new ControlledClient();
        client.Release.TrySetResult();
        await new CoachingSession(new FakeSource(clock), client, clock).RunAsync(Profile, 1, progress, default);
        Assert.Equal(1, client.Calls);
        Assert.Single(progress.Updates, update => update.Advice?.IsFinal == true);
        Assert.False(progress.Updates.Last().ClearAdvice);
    }

    [Fact]
    public async Task ExpiredInferenceIsCancelledAndNeverFinalized()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        var progress = new Collector();
        var run = new CoachingSession(new FakeSource(clock), client, clock).RunAsync(Profile, 1, progress, default);
        await client.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        clock.Advance(TimeSpan.FromSeconds(11));
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.DoesNotContain(progress.Updates, update => update.Advice?.IsFinal == true);
        Assert.Contains(progress.Updates, update => update.Status == "Respuesta caducada");
    }

    [Fact]
    public async Task NextSnapshotIsCapturedImmediatelyAfterReplyWithoutATimer()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        var progress = new Collector();
        var priorAdvice = Enumerable.Range(1, 6).Select(index => $"Ataca objetivo {index}.").ToArray();
        var run = new CoachingSession(new FakeSource(clock), client, clock, priorAdvice).RunAsync(Profile, 2, progress, default);
        Assert.Equal(0, await client.Started.Reader.ReadAsync());
        clock.Advance(TimeSpan.FromSeconds(2));
        await progress.Samples.Reader.ReadAsync();
        Assert.False(progress.Samples.Reader.TryRead(out _));
        client.Release.TrySetResult();
        Assert.Equal(1, await client.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)));
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, client.MaxActive);
        Assert.Equal(2, client.Calls);
        Assert.Equal(priorAdvice.TakeLast(5), client.History[0]);
        Assert.Equal(priorAdvice.TakeLast(4).Append("Ataca súbdito."), client.History[1]);
    }

    [Fact]
    public async Task VoiceCompletionGatesTheNextObservationAndModelRequest()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        client.Release.TrySetResult();
        var progress = new Collector();
        var waitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var speechFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = new CoachingSession(new FakeSource(clock), client, clock, waitForSpeech: cancellationToken =>
        {
            waitStarted.TrySetResult();
            return speechFinished.Task.WaitAsync(cancellationToken);
        }).RunAsync(Profile, 2, progress, default);

        await progress.Samples.Reader.ReadAsync();
        await progress.Finals.Reader.ReadAsync();
        await waitStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, client.Calls);
        Assert.False(progress.Samples.Reader.TryRead(out _));

        speechFinished.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(2, client.Calls);
        Assert.True(progress.Samples.Reader.TryRead(out _));
    }

    [Fact]
    public async Task StoppingDuringSpeechWaitPreventsAnotherObservation()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        client.Release.TrySetResult();
        var progress = new Collector();
        var waitStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var speechFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        var run = new CoachingSession(new FakeSource(clock), client, clock, waitForSpeech: cancellationToken =>
        {
            waitStarted.TrySetResult();
            return speechFinished.Task.WaitAsync(cancellationToken);
        }).RunAsync(Profile, null, progress, stop.Token);

        await progress.Finals.Reader.ReadAsync();
        await waitStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.Equal(1, client.Calls);
        Assert.Single(progress.Updates, update => update.Observation is not null);
    }

    [Fact]
    public async Task StopCancelsPartialStreamAndSuppressesFinalAdvice()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        var progress = new Collector();
        using var stop = new CancellationTokenSource();
        var run = new CoachingSession(new FakeSource(clock), client, clock).RunAsync(Profile, 12, progress, stop.Token);
        await client.Started.Reader.ReadAsync();
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.DoesNotContain(progress.Updates, update => update.Advice?.IsFinal == true);
        Assert.True(progress.Updates.Last().ClearAdvice);
    }

    [Fact]
    public async Task LosingBotVerificationBeforeNextRequestStopsFurtherCalls()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        var progress = new Collector();
        var run = new CoachingSession(new FailingSource(clock), client, clock).RunAsync(Profile, 12, progress, default);
        await client.Started.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        client.Release.TrySetResult();
        await Assert.ThrowsAsync<PracticeUnavailableException>(() => run.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, client.Calls);
        Assert.Single(progress.Updates, update => update.Advice?.IsFinal == true);
        Assert.True(progress.Updates.Last().ClearAdvice);
    }

    [Fact]
    public async Task ContinuousModeExceedsFiniteDefaultAndOnlyStopsOnCancellation()
    {
        var clock = new FakeTimeProvider();
        var client = new ControlledClient();
        client.Release.TrySetResult();
        var progress = new Collector();
        using var stop = new CancellationTokenSource();
        var run = new CoachingSession(new FakeSource(clock), client, clock).RunAsync(Profile, null, progress, stop.Token);
        for (var index = 0; index < 15; index++)
        {
            await progress.Finals.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3));
        }
        Assert.True(client.Calls >= 15);
        Assert.False(run.IsCompleted);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        Assert.True(progress.Updates.Last().ClearAdvice);
        Assert.DoesNotContain(progress.Updates, update => update.Status == "Límite de solicitudes alcanzado");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(721)]
    public async Task InvalidFiniteLimitsRemainRejected(int limit)
    {
        var clock = new FakeTimeProvider();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new CoachingSession(new FakeSource(clock), new ControlledClient(), clock)
                .RunAsync(Profile, limit, new Collector(), default));
    }

    private sealed class FailingSource(TimeProvider clock) : IObservationSource
    {
        private bool sampled;
        public Task<Observation> CaptureAsync(CancellationToken cancellationToken)
        {
            if (sampled) return Task.FromException<Observation>(new PracticeUnavailableException("Rivales no verificados."));
            sampled = true;
            return Task.FromResult(TestObservations.Create(new(0, 1, 200, 600, 500), clock.GetTimestamp()));
        }
    }

    private sealed class FakeSource(TimeProvider clock) : IObservationSource
    {
        private int sequence;
        public Task<Observation> CaptureAsync(CancellationToken cancellationToken)
            => Task.FromResult(TestObservations.Create(new(sequence++, 1, 200, 600, 500), clock.GetTimestamp()));
    }

    private sealed class Collector : IProgress<SessionUpdate>
    {
        public List<SessionUpdate> Updates { get; } = [];
        public Channel<bool> Samples { get; } = Channel.CreateUnbounded<bool>();
        public Channel<bool> Finals { get; } = Channel.CreateUnbounded<bool>();
        public void Report(SessionUpdate update)
        {
            Updates.Add(update);
            if (update.Observation is not null) Samples.Writer.TryWrite(true);
            if (update.Advice?.IsFinal == true) Finals.Writer.TryWrite(true);
        }
    }

    private sealed class ControlledClient : ICoachingClient
    {
        public int Calls { get; private set; }
        private int active;
        public int MaxActive { get; private set; }
        public Channel<int> Started { get; } = Channel.CreateUnbounded<int>();
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string[]> History { get; } = [];

        public async IAsyncEnumerable<string> StreamAsync(Observation observation, PlayerProfile profile,
            IReadOnlyList<string> recentAdvice, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Calls++;
            History.Add(recentAdvice.ToArray());
            MaxActive = Math.Max(MaxActive, ++active);
            try
            {
                Started.Writer.TryWrite((int)observation.State.GameTime!);
                yield return "Ataca ";
                await Release.Task.WaitAsync(cancellationToken);
                yield return "súbdito.";
            }
            finally { active--; }
        }
    }
}