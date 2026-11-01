using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LoLCoach.Models;
using LoLCoach.Services;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace LoLCoach.Tests;

public class LiveObservationTests
{
    private static PracticeState State(TimeProvider clock)
    {
        using var active = JsonDocument.Parse("""{"riotId":"private#local"}""");
        using var roster = JsonDocument.Parse("""[{"riotId":"private#local","team":"ORDER","isBot":false,"championName":"Garen"},{"team":"CHAOS","isBot":true}]""");
        return new(new(150, 3, 500, 800, 600), BotMatchVerifier.Verify(active.RootElement, roster.RootElement), clock.GetTimestamp());
    }

    private static CapturedWindowFrame Frame(TimeProvider clock, bool black = false)
    {
        var pixels = Enumerable.Repeat(black ? (byte)0 : (byte)255, 400 * 240 * 4).ToArray();
        return new(WindowCaptureService.CreateFrame(400, 240, pixels, 1600), clock.GetTimestamp());
    }

    [Fact]
    public async Task VerifiedFreshCaptureProducesJpegWithOnlyOwnStatistics()
    {
        var clock = new FakeTimeProvider();
        var reader = new StateReader(State(clock));
        var capture = new FrameSource(Frame(clock));
        var observation = await new LiveObservationSource(capture, reader, clock).CaptureAsync(default);
        Assert.Equal(ObservationKind.BotPractice, observation.Kind);
        Assert.Equal("Garen", observation.Practice!.Champion);
        Assert.Equal(500, observation.State.Health);
        Assert.NotEmpty(observation.Jpeg);
        Assert.DoesNotContain("private", JsonSerializer.Serialize(observation.State));
        Assert.Equal(400, ImageCodec.Decode(observation.Jpeg).PixelWidth);
        Assert.Equal(1, reader.Calls);
        Assert.Equal(1, capture.Calls);
    }

    [Fact]
    public async Task OtherApplicationsAreRejectedBeforeLocalApiRead()
    {
        var clock = new FakeTimeProvider();
        var reader = new StateReader(State(clock));
        var capture = new FrameSource(Frame(clock)) { IsGameWindow = false };
        await Assert.ThrowsAsync<PracticeUnavailableException>(() => new LiveObservationSource(capture, reader, clock).CaptureAsync(default));
        Assert.Equal(0, reader.Calls);
        Assert.Equal(0, capture.Calls);
    }

    [Fact]
    public async Task OldFramesAreNeverUploadedAsNewObservations()
    {
        var clock = new FakeTimeProvider();
        var capture = new FrameSource(Frame(clock));
        clock.Advance(TimeSpan.FromSeconds(4));
        await Assert.ThrowsAsync<PracticeUnavailableException>(() => new LiveObservationSource(capture,
            new StateReader(State(clock)), clock).CaptureAsync(default));
    }

    [Fact]
    public async Task UnsynchronizedStateAndFrameFailClosed()
    {
        var clock = new FakeTimeProvider();
        var reader = new StateReader(State(clock));
        clock.Advance(TimeSpan.FromMilliseconds(1500));
        await Assert.ThrowsAsync<PracticeUnavailableException>(() => new LiveObservationSource(new FrameSource(Frame(clock)), reader, clock).CaptureAsync(default));
    }

    [Fact]
    public async Task BlackFramesAreRejected()
    {
        var clock = new FakeTimeProvider();
        await Assert.ThrowsAsync<PracticeUnavailableException>(() => new LiveObservationSource(new FrameSource(Frame(clock, black: true)),
            new StateReader(State(clock)), clock).CaptureAsync(default));
    }

    [Fact]
    public async Task VerificationFailureStopsObservation()
    {
        var clock = new FakeTimeProvider();
        await Assert.ThrowsAsync<PracticeUnavailableException>(() => new LiveObservationSource(new FrameSource(Frame(clock)),
            new RejectedStateReader(), clock).CaptureAsync(default));
    }

    private sealed class StateReader(PracticeState state) : IPracticeStateReader
    {
        public int Calls { get; private set; }
        public Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(state); }
    }

    private sealed class RejectedStateReader : IPracticeStateReader
    {
        public Task<PracticeState> ReadPracticeAsync(CancellationToken cancellationToken)
            => Task.FromException<PracticeState>(new PracticeUnavailableException("No se confirman bots."));
    }

    private sealed class FrameSource(CapturedWindowFrame frame) : IPracticeFrameSource
    {
        public bool IsGameWindow { get; init; } = true;
        public int Calls { get; private set; }
        public Task<CapturedWindowFrame> CaptureFreshAsync(CancellationToken cancellationToken)
        { Calls++; return Task.FromResult(frame); }
    }
}