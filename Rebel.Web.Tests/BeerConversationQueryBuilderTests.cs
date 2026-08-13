using Rebel.Web.Models;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerConversationQueryBuilderTests
{
    private readonly BeerConversationQueryBuilder _builder =
        new(new BeerPreferenceParser());

    [Fact]
    public void Build_CarriesEarlierTasteIntoShortRefinement()
    {
        var history = new List<BeerChatTurn>
        {
            User("Show me three grapefruit IPAs.")
        };

        var result = _builder.Build("Something less bitter.", history);

        Assert.Contains("ipa", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("grapefruit", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("less bitter", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("three", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_UsesNewestStyleInsteadOfContradictingIt()
    {
        var history = new List<BeerChatTurn>
        {
            User("I want a citrus IPA.")
        };

        var result = _builder.Build("Actually, make it a stout.", history);

        Assert.Contains("stout", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ipa", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_UsesNewestAbvLimit()
    {
        var history = new List<BeerChatTurn>
        {
            User("Find an IPA under 6% ABV.")
        };

        var result = _builder.Build("Actually, over 7%.", history);

        Assert.Contains("over 7%", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("under 6", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ipa", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_IgnoresAssistantWordsAsGuestPreferences()
    {
        var history = new List<BeerChatTurn>
        {
            User("I want a lager."),
            new() { Role = "assistant", Text = "You might also enjoy a coffee stout." }
        };

        var result = _builder.Build("Make it less bitter.", history);

        Assert.Contains("lager", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stout", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("coffee", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_CarriesFoodAndStrengthAcrossSeveralTurns()
    {
        var history = new List<BeerChatTurn>
        {
            User("I need beer for a burger."),
            User("Keep it under 5% ABV."),
            User("A crisp lager sounds right.")
        };

        var result = _builder.Build("Show me two.", history);

        Assert.Contains("burger", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("under 5%", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lager", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("crisp", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_CarriesBudgetWithoutTurningItIntoAbv()
    {
        var history = new List<BeerChatTurn>
        {
            User("Keep it under 300 denars."),
            User("I want a citrus IPA.")
        };

        var result = _builder.Build("Make it less bitter.", history);

        Assert.Contains("under 300 denars", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ipa", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("citrus", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_AnyStyleRecoveryDropsEarlierStyle()
    {
        var result = _builder.Build(
            "Keep the flavour and strength preferences, but show me any beer style that fits.",
            [User("I want a grapefruit stout under 7% ABV.")]);

        Assert.Contains("grapefruit", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("under 7%", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stout", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_MediumBitternessRecoveryDropsLowBitternessConstraint()
    {
        var result = _builder.Build(
            "Keep my other preferences, but medium bitterness is okay.",
            [User("I want an IPA that is not bitter.")]);

        Assert.Contains("ipa", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not bitter", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("sour?")]
    [InlineData("sour beer")]
    [InlineData("stout")]
    public void Build_StandaloneStyleStartsFreshInsteadOfCarryingOldConstraints(string message)
    {
        var result = _builder.Build(
            message,
            [User("I want a German grapefruit IPA under 5% ABV that is not bitter.")]);

        Assert.Equal(message, result);
        Assert.DoesNotContain("grapefruit", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("under 5", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("German", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Hungarian beer")]
    [InlineData("show me Hungarian beers")]
    public void Build_StandaloneOriginStartsFreshInsteadOfCarryingOldTaste(string message)
    {
        var result = _builder.Build(
            message,
            [User("I want a dark German stout that is not sweet.")]);

        Assert.Equal(message, result);
        Assert.DoesNotContain("stout", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("German", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("no, not sour, i want high %abv beer now")]
    [InlineData("not sour. show me the strongest beers")]
    public void Build_ExplicitlyRejectedStyleDoesNotRemainInConversation(string message)
    {
        var result = _builder.Build(
            message,
            [User("sour beer")]);

        Assert.DoesNotContain("earlier preferences", result, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(message, result);
    }

    [Fact]
    public void Build_HighestAlcoholRequestStartsFreshAfterUnrelatedMistake()
    {
        const string message = "let's drink some high %abv beers. recommend me 4 of the highest alcohol beers you have";

        var result = _builder.Build(message, [User("sour beer")]);

        Assert.Equal(message, result);
        Assert.DoesNotContain("sour", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("now yuzu flavoured beer?")]
    [InlineData("instead, mango beer")]
    [InlineData("how about a stout?")]
    public void Build_ClearNewDirectionDoesNotCarryPreviousBeerPreferences(string message)
    {
        var result = _builder.Build(
            message,
            [User("I want a local stout.")]);

        Assert.Equal(message, result);
        Assert.DoesNotContain("local", result, StringComparison.OrdinalIgnoreCase);
        if (!message.Contains("stout", StringComparison.OrdinalIgnoreCase))
        {
            Assert.DoesNotContain("stout", result, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static BeerChatTurn User(string text) => new()
    {
        Role = "user",
        Text = text
    };
}
