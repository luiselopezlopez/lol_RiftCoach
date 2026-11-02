using System.Text.Json;
using LoLCoach.Models;
using Xunit;

namespace LoLCoach.Tests;

public class BotPracticeTests
{
    private const string Active = """{"riotId":"local#test","level":3}""";
    private const string Local = """{"riotId":"local#test","team":"ORDER","isBot":false,"championName":"Garen"}""";
    private const string Ally = """{"riotId":"ally#test","team":"ORDER","isBot":false}""";
    private const string Bot = """{"team":"CHAOS","isBot":true}""";

    private static VerifiedBotPractice Verify(string active, string players)
    {
        using var activeJson = JsonDocument.Parse(active);
        using var playersJson = JsonDocument.Parse(players);
        return BotMatchVerifier.Verify(activeJson.RootElement, playersJson.RootElement);
    }

    [Fact]
    public void CoopWithHumanAlliesAndOnlyBotOpponentsIsRecognized()
    {
        var verified = Verify(Active, $"[{Local},{Ally},{Bot},{Bot}]");
        Assert.Equal("Garen", verified.Champion);
        var observation = new Observation([], new(null, null, null, null, null), 0, ObservationKind.BotPractice) { Practice = verified };
        ProductPolicy.EnsureAllowed(observation);
    }

    [Theory]
    [InlineData("{\"team\":\"CHAOS\",\"isBot\":false}")]
    [InlineData("{\"team\":\"CHAOS\"}")]
    [InlineData("{\"team\":\"CHAOS\",\"isBot\":\"true\"}")]
    [InlineData("{\"team\":\"UNKNOWN\",\"isBot\":true}")]
    [InlineData("null")]
    public void HumanUnknownAndMalformedOpponentsFailClosed(string opponent)
        => Assert.Throws<PracticeUnavailableException>(() => Verify(Active, $"[{Local},{Bot},{opponent}]"));

    [Theory]
    [InlineData("{\"team\":\"CHAOS\",\"isBot\":false}")]
    [InlineData("{\"team\":\"CHAOS\"}")]
    [InlineData("{\"team\":\"CHAOS\",\"isBot\":\"true\"}")]
    public void DebugOverrideAllowsUnverifiedOpponentsButMarksTheResult(string opponent)
    {
        using var activeJson = JsonDocument.Parse(Active);
        using var playersJson = JsonDocument.Parse($"[{Local},{opponent}]");

        var result = BotMatchVerifier.Verify(activeJson.RootElement, playersJson.RootElement,
            allowUnverifiedBotOpponentsForDebugging: true);

        Assert.Equal("Garen", result.Champion);
        Assert.True(result.BotVerificationBypassed);
    }

    [Fact]
    public void DebugOverrideStillRequiresLocalHumanAndAtLeastOneOpponent()
    {
        using var activeJson = JsonDocument.Parse(Active);
        using var localBot = JsonDocument.Parse("""[{"riotId":"local#test","team":"ORDER","isBot":true,"championName":"Garen"},{"team":"CHAOS"}]""");
        using var noOpponents = JsonDocument.Parse($"[{Local},{Ally}]");

        Assert.Throws<PracticeUnavailableException>(() => BotMatchVerifier.Verify(activeJson.RootElement,
            localBot.RootElement, allowUnverifiedBotOpponentsForDebugging: true));
        Assert.Throws<PracticeUnavailableException>(() => BotMatchVerifier.Verify(activeJson.RootElement,
            noOpponents.RootElement, allowUnverifiedBotOpponentsForDebugging: true));
    }

    [Fact]
    public void MissingLocalIdentityEmptyRosterAndNoOpponentsFailClosed()
    {
        Assert.Throws<PracticeUnavailableException>(() => Verify("{}", $"[{Local},{Bot}]"));
        Assert.Throws<PracticeUnavailableException>(() => Verify(Active, "[]"));
        Assert.Throws<PracticeUnavailableException>(() => Verify(Active, $"[{Local},{Ally}]"));
        Assert.Throws<PracticeUnavailableException>(() => Verify(Active, $"[{Local},{Local},{Bot}]"));
        Assert.Throws<PracticeUnavailableException>(() => Verify(Active, "{}"));
    }

    [Fact]
    public void ObservationCannotClaimBotPracticeWithoutVerification()
        => Assert.Throws<PracticeUnavailableException>(() => ProductPolicy.EnsureAllowed(
            new Observation([], new(null, null, null, null, null), 0, ObservationKind.BotPractice)));

    [Fact]
    public void PvpRemainsBlocked()
        => Assert.Throws<InvalidOperationException>(() => ProductPolicy.EnsureAllowed(ObservationKind.LiveGame));
}