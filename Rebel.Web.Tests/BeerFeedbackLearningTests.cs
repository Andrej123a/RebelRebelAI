using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Models;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public class BeerFeedbackLearningTests
{
    private readonly BeerPreferenceParser _parser = new();
    private readonly BeerFeedbackLearningService _learning = new();

    [Fact]
    public void Parser_ExtractsStructuredTasteWithoutKeepingRawMessage()
    {
        var result = _parser.Parse(
            "I want a citrussy grapefruit IPA with a burger");

        Assert.Equal("ipa", result.Style);
        Assert.Contains("citrus", result.Flavours);
        Assert.Contains("grapefruit", result.Flavours);
        Assert.Equal("burger", result.FoodPairing);
    }

    [Fact]
    public void Parser_ExtractsOriginStrengthAndLowBitterness()
    {
        var result = _parser.Parse(
            "A strong German beer, but not too bitter");

        Assert.Equal("germany", result.Origin);
        Assert.Equal("high", result.Strength);
        Assert.Equal("low", result.Bitterness);
    }

    [Theory]
    [InlineData(BeerFeedbackReason.TooBitter, "Bitterness", "low")]
    [InlineData(BeerFeedbackReason.TooSweet, "Sweetness", "low")]
    [InlineData(BeerFeedbackReason.TooStrong, "Strength", "low")]
    [InlineData(BeerFeedbackReason.TooWeak, "Strength", "high")]
    public void Parser_CorrectionOverridesPreference(
        BeerFeedbackReason correction,
        string property,
        string expected)
    {
        var result = _parser.Parse("beer", correction);
        var actual = property switch
        {
            "Bitterness" => result.Bitterness,
            "Sweetness" => result.Sweetness,
            _ => result.Strength
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Learning_PositiveFeedbackRewardsMatchingContextMore()
    {
        var beerId = Guid.NewGuid();
        var feedback = Feedback(
            beerId,
            isPositive: true,
            style: "ipa",
            flavours: "citrus,grapefruit");

        var matching = _learning.CalculateScores(
            new BeerPreferenceFingerprint(
                "ipa", ["citrus"], null, null, null, null, null),
            [feedback]);
        var unrelated = _learning.CalculateScores(
            new BeerPreferenceFingerprint(
                "stout", ["coffee"], null, null, null, null, null),
            [feedback]);

        Assert.True(matching[beerId] > unrelated[beerId]);
    }

    [Fact]
    public void Learning_TooBitterOnlyPenalizesLowBitternessIntent()
    {
        var beerId = Guid.NewGuid();
        var feedback = Feedback(
            beerId,
            isPositive: false,
            reason: BeerFeedbackReason.TooBitter,
            bitterness: "low");

        var low = _learning.CalculateScores(Profile(bitterness: "low"), [feedback]);
        var high = _learning.CalculateScores(Profile(bitterness: "high"), [feedback]);

        Assert.True(low[beerId] < 0);
        Assert.Equal(0, high[beerId]);
    }

    [Fact]
    public void Learning_AlreadyTriedDoesNotChangeGlobalRanking()
    {
        var beerId = Guid.NewGuid();
        var feedback = Feedback(
            beerId,
            isPositive: false,
            reason: BeerFeedbackReason.AlreadyTried,
            style: "ipa");

        var scores = _learning.CalculateScores(
            new BeerPreferenceFingerprint(
                "ipa", [], null, null, null, null, null),
            [feedback]);

        Assert.Equal(0, scores[beerId]);
    }

    private static BeerPreferenceFingerprint Profile(string? bitterness = null) =>
        new(null, [], null, null, bitterness, null, null);

    private static BeerGuideFeedback Feedback(
        Guid productId,
        bool isPositive,
        BeerFeedbackReason? reason = null,
        string? style = null,
        string? flavours = null,
        string? bitterness = null) => new()
    {
        Id = Guid.NewGuid(),
        ResponseId = Guid.NewGuid(),
        ProductId = productId,
        AnonymousSessionHash = new string('A', 64),
        IsPositive = isPositive,
        Reason = reason,
        RequestedStyle = style,
        FlavourTags = flavours,
        BitternessPreference = bitterness,
        CreatedAtUtc = DateTime.UtcNow
    };
}
