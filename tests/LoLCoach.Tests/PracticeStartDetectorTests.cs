using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;
using LoLCoach.Models;
using LoLCoach.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace LoLCoach.Tests;

public class PracticeStartDetectorTests
{
    private static PracticeState VerifiedState(TimeProvider clock)
    {
        using var active = JsonDocument.Parse("""{"riotId":"local#test"}""");
        using var players = JsonDocument.Parse("""[{"riotId":"local#test","team":"ORDER","isBot":false,"championName":"Garen"},{"team":"CHAOS","isBot":true}]""");
        return new(new(1, 1, 600, 600, 500), BotMatchVerifier.Verify(active.RootElement, players.RootElement), clock.GetTimestamp());
    }

    [Fact]
    public async Task WaitsLocallyUntilGameStartsAndReturnsExactlyOnce()
    {
        var clock = new FakeTimeProvider();
        var reader = new Reader(() => VerifiedState(clock));
        var progress = new Collector();
        var waiting = new PracticeStartDetector(reader, clock).WaitAsync(progress, default);
        Assert.False(waiting.IsCompleted);
        Assert.Equal(1, reader.Calls);
        await progress.Messages.Reader.ReadAsync();
        reader.Ready = true;
        clock.Advance(PracticeStartDetector.PollInterval);
        var state = await waiting.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal("Garen", state.Verification.Champion);
        Assert.Equal(2, reader.Calls);
        clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Equal(2, reader.Calls);
    }

    [Fact]
    public async Task PvpOrUnknownRivalsKeepWaitingWithoutStartingASession()
    {
        var clock = new FakeTimeProvider();
        var reader = new Reader(() => VerifiedState(clock)) { Unverified = true };
        using var stop = new CancellationTokenSource();
        var progress = new Collector();
        var waiting = new PracticeStartDetector(reader, clock).WaitAsync(progress, stop.Token);
        Assert.Contains("bot", await progress.Messages.Reader.ReadAsync());
        Assert.False(waiting.IsCompleted);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(1, reader.Calls);
    }

    [Fact]
    public async Task CancelledBeforePollingNeverReadsLocalApi()
    {
        var clock = new FakeTimeProvider();
        var reader = new Reader(() => VerifiedState(clock));
        using var stop = new CancellationTokenSource();
        stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new PracticeStartDetector(reader, clock)
            .WaitAsync(new Collector(), stop.Token));
        Assert.Equal(0, reader.Calls);
    }

    [Fact]
    public async Task AlreadyRunningBotGameStartsWithoutWaitingForAnotherPoll()
    {
        var clock = new FakeTimeProvider();
        var reader = new Reader(() => VerifiedState(clock)) { Ready = true };
        var state = await new PracticeStartDetector(reader, clock).WaitAsync(new Collector(), default);
        Assert.Equal(1, reader.Calls);
        Assert.Equal("Garen", state.Verification.Champion);
    }

    private sealed class Collector : IProgress<string>
    {
        public Channel<string> Messages { get; } = Channel.CreateUnbounded<string>();
        public void Report(string value) => Messages.Writer.TryWrite(value);
    }

    private sealed class Reader(Func<PracticeState> state) : IPracticeStateReader
    {
        public bool Ready { get; set; }
        public bool Unverified { get; init; }
        public int Calls { get; private set; }
        public Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken)
        {
            Calls++;
            if (Unverified) return Task.FromException<PracticeState>(new PracticeUnavailableException("No se confirman bots."));
            if (!Ready) return Task.FromException<PracticeState>(new HttpRequestException(HttpRequestError.ConnectionError, "No game client."));
            return Task.FromResult(state());
        }
    }
}