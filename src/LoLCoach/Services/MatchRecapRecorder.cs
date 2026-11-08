using LoLCoach.Models;

namespace LoLCoach.Services;

public sealed record MatchRecapSample(double GameTime, string? GameMode, string? MapName,
    IReadOnlyList<RosterPlayer> Players);

public sealed record MatchRecapAdvice(double? GameTime, string Text);

public enum MatchRecapEndEvidence
{
    LiveClientDataUnavailableAfterRetries,
    PlayerIndicatedMatchEnded
}

public sealed record MatchRecapInput(string Champion, string Role, MatchRecapEndEvidence EndEvidence,
    IReadOnlyList<MatchRecapSample> Timeline, IReadOnlyList<MatchRecapAdvice> Advice);

public sealed class MatchRecapRecorder(string champion, string role)
{
    private const double SampleIntervalSeconds = 60;
    private const int MaximumSamples = 120;
    private const int MaximumAdvice = CoachingSession.MaxHistory;
    private readonly List<MatchRecapSample> timeline = [];
    private readonly Queue<MatchRecapAdvice> advice = new();
    private double? lastObservedGameTime;
    private GameSnapshot? lastObservedSnapshot;
    public double? LastGameTime => lastObservedGameTime;

    public void Observe(Observation observation, bool force = false) => Observe(observation.State, force);

    public void Observe(GameSnapshot snapshot, bool force = false)
    {
        if (snapshot.GameTime is not { } gameTime || !double.IsFinite(gameTime) || snapshot.Roster.Count == 0)
            return;
        lastObservedGameTime = gameTime;
        lastObservedSnapshot = snapshot;
        if (timeline.Count > 0 && gameTime == timeline[^1].GameTime)
        {
            if (force) timeline[^1] = CreateSample(snapshot, gameTime);
            return;
        }
        if (!force && timeline.Count > 0 && gameTime - timeline[^1].GameTime < SampleIntervalSeconds) return;

        timeline.Add(CreateSample(snapshot, gameTime));
        while (timeline.Count > MaximumSamples)
            timeline.RemoveAt(1);
    }

    public void ForceLatestSample()
    {
        if (lastObservedSnapshot is { } snapshot) Observe(snapshot, force: true);
    }

    private static MatchRecapSample CreateSample(GameSnapshot snapshot, double gameTime)
        => new(gameTime, snapshot.GameMode, snapshot.MapName,
            snapshot.Roster.Select(player => player with { Items = player.Items.ToArray() }).ToArray());

    public void RecordAdvice(string text)
    {
        if (!AdviceValidator.IsValidForHistory(text)) return;
        advice.Enqueue(new(lastObservedGameTime, text.Trim()));
        while (advice.Count > MaximumAdvice) advice.Dequeue();
    }

    public MatchRecapInput Build(MatchRecapEndEvidence endEvidence) => new(champion, role, endEvidence,
        timeline.ToArray(), advice.ToArray());
}
