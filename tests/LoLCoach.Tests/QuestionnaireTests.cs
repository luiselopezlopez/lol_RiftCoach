using LoLCoach.Services;
using Xunit;

namespace LoLCoach.Tests;

public class QuestionnaireTests
{
    [Fact]
    public void ResilientDuelistPrefersTop()
    {
        var result = QuestionnaireService.Evaluate([0, 0, 0, 0, 0, 0, 1, 0]);
        Assert.Equal(PlayerRole.Top, result.Roles[0]);
        Assert.Equal(3, result.Champions.Length);
        Assert.Equal(3, result.Champions.Distinct().Count());
        Assert.All(result.Reasons, reason => Assert.False(string.IsNullOrWhiteSpace(reason)));
    }

    [Fact]
    public void ProtectorPrefersSupport()
        => Assert.Equal(PlayerRole.Support, QuestionnaireService.Evaluate([2, 2, 2, 0, 2, 2, 2, 2]).Roles[0]);

    [Fact]
    public void MapAndRoutesPreferJungle()
        => Assert.Equal(PlayerRole.Jungle, QuestionnaireService.Evaluate([0, 1, 0, 1, 2, 1, 0, 1]).Roles[0]);

    [Fact]
    public void RangedAttackerPrefersAdc()
        => Assert.Equal(PlayerRole.Adc, QuestionnaireService.Evaluate([1, 0, 1, 1, 1, 0, 1, 0]).Roles[0]);

    [Fact]
    public void InvalidAndIncompleteAnswersAreRejected()
    {
        Assert.Throws<ArgumentException>(() => QuestionnaireService.Evaluate([]));
        Assert.Throws<ArgumentException>(() => QuestionnaireService.Evaluate([-1, 0, 0, 0, 0, 0, 0, 0]));
        Assert.Throws<ArgumentException>(() => QuestionnaireService.Evaluate([3, 0, 0, 0, 0, 0, 0, 0]));
    }

    [Fact]
    public void EveryPossibleProfileHasStableAndValidRecommendations()
    {
        for (var profileIndex = 0; profileIndex < 6561; profileIndex++)
        {
            var value = profileIndex;
            var answers = new int[8];
            for (var answerIndex = 0; answerIndex < answers.Length; answerIndex++)
            {
                answers[answerIndex] = value % 3;
                value /= 3;
            }
            var result = QuestionnaireService.Evaluate(answers);
            Assert.Equal(result.Roles, QuestionnaireService.Evaluate(answers).Roles);
            Assert.Equal(2, result.Roles.Distinct().Count());
            Assert.Equal(3, result.Champions.Distinct().Count());
            Assert.All(result.Champions, champion => Assert.Contains(champion.Role, result.Roles));
        }
    }
}