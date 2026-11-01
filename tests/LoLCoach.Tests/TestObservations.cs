using System.Text.Json;
using LoLCoach.Models;

namespace LoLCoach.Tests;

internal static class TestObservations
{
    public static VerifiedBotPractice Practice { get; } = Verify();
    private static VerifiedBotPractice Verify()
    {
        using var active = JsonDocument.Parse("""{"riotId":"local#test"}""");
        using var players = JsonDocument.Parse("""[{"riotId":"local#test","team":"ORDER","isBot":false,"championName":"Garen"},{"team":"CHAOS","isBot":true}]""");
        return BotMatchVerifier.Verify(active.RootElement, players.RootElement);
    }
    public static Observation Create(GameSnapshot state, long timestamp = 0, byte[]? jpeg = null)
        => new(jpeg ?? [], state, timestamp, ObservationKind.BotPractice) { Practice = Practice };
}