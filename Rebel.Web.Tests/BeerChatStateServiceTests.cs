using Rebel.Web.Models;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerChatStateServiceTests
{
    private readonly BeerChatStateService _service =
        new(new BeerPreferenceParser());

    [Fact]
    public void Update_RefinementKeepsExplicitExistingPreferences()
    {
        var previous = new BeerChatPreferenceState
        {
            Style = "ipa",
            Origin = "hungary",
            Flavours = ["grapefruit"]
        };

        var result = _service.Update("make it less bitter", previous);

        Assert.Equal("ipa", result.Preferences.Style);
        Assert.Equal("hungary", result.Preferences.Origin);
        Assert.Contains("grapefruit", result.Preferences.Flavours);
        Assert.Equal("low", result.Preferences.Bitterness);
        Assert.Contains("not bitter", result.EffectiveQuery);
    }

    [Fact]
    public void Update_NewDirectionClearsUnrelatedPreviousPreferences()
    {
        var previous = new BeerChatPreferenceState
        {
            Style = "stout",
            Origin = "local",
            Flavours = ["coffee"]
        };

        var result = _service.Update("now yuzu flavoured beer", previous);

        Assert.Null(result.Preferences.Style);
        Assert.Null(result.Preferences.Origin);
        Assert.Equal(["yuzu"], result.Preferences.Flavours);
        Assert.DoesNotContain("stout", result.EffectiveQuery);
        Assert.DoesNotContain("local", result.EffectiveQuery);
    }

    [Fact]
    public void Update_RejectedStyleRemovesAndExcludesIt()
    {
        var previous = new BeerChatPreferenceState { Style = "sour" };

        var result = _service.Update("no, not sour, show me strong beer", previous);

        Assert.Null(result.Preferences.Style);
        Assert.Contains("sour", result.Preferences.ExcludedStyles);
        Assert.Contains("not sour", result.EffectiveQuery);
        Assert.Equal("high", result.Preferences.Strength);
    }

    [Fact]
    public void Update_ObjectiveRequestStartsFreshAndCapturesSortAndCount()
    {
        var previous = new BeerChatPreferenceState
        {
            Style = "sour",
            Origin = "hungary"
        };

        var result = _service.Update(
            "show me 4 of the highest alcohol beers",
            previous);

        Assert.Null(result.Preferences.Style);
        Assert.Null(result.Preferences.Origin);
        Assert.Equal(4, result.Preferences.RequestedCount);
        Assert.Equal("highest-abv", result.Preferences.Sort);
        Assert.Contains("highest alcohol", result.EffectiveQuery);
    }

    [Fact]
    public void Update_RefreshingRequestPreservesPreferenceForCatalogRanking()
    {
        var result = _service.Update(
            "Show me three refreshing beers for a hot day.",
            null);

        Assert.Equal(3, result.Preferences.RequestedCount);
        Assert.Contains("refreshing", result.Preferences.Flavours);
        Assert.Contains("refreshing", result.EffectiveQuery);
    }

    [Fact]
    public void Update_PriceTargetSurvivesBeerOrFoodClarification()
    {
        var initial = _service.Update("something around 300 MKD", null);
        var clarified = _service.Update("Beer", initial.Preferences);

        Assert.Equal(300m, clarified.Preferences.TargetPrice);
        Assert.Contains("around 300 MKD", clarified.EffectiveQuery);
    }

    [Fact]
    public void Update_PriceRangeAndTierArePreserved()
    {
        var range = _service.Update("food between 250 and 400 MKD", null);
        var tier = _service.Update("$$ beer", null);

        Assert.Equal(250m, range.Preferences.MinimumPrice);
        Assert.Equal(400m, range.Preferences.MaximumPrice);
        Assert.Contains("between 250 and 400 MKD", range.EffectiveQuery);
        Assert.Equal("mid-range", tier.Preferences.PriceTier);
    }

    [Fact]
    public void Update_NamedProfileQuestionClearsOldPriceDirection()
    {
        var previous = new BeerChatPreferenceState
        {
            MinimumPrice = 400m,
            Sort = "highest-price"
        };

        var result = _service.Update("explain me the flavor of Abasar", previous);

        Assert.Null(result.Preferences.MinimumPrice);
        Assert.Null(result.Preferences.Sort);
        Assert.Equal("explain me the flavor of Abasar", result.EffectiveQuery);
    }

    [Fact]
    public void Update_NormalizesUntrustedBrowserState()
    {
        var previous = new BeerChatPreferenceState
        {
            Style = new string('x', 100),
            RequestedCount = 99,
            MaximumAbv = 200,
            ExcludedStyles = ["sour", "invented"]
        };

        var result = _service.Update("make it less bitter", previous);

        Assert.Equal(30, result.Preferences.Style!.Length);
        Assert.Null(result.Preferences.RequestedCount);
        Assert.Null(result.Preferences.MaximumAbv);
        Assert.Equal(["sour"], result.Preferences.ExcludedStyles);
    }

    [Fact]
    public void Update_MixedOrderRemembersSeparateCountsAndTotalBudget()
    {
        var result = _service.Update(
            "I've got 1000 to spend, give me a food and 2 beers",
            null);

        Assert.Equal("mixed", result.Preferences.ItemKind);
        Assert.Equal(1, result.Preferences.RequestedFoodCount);
        Assert.Equal(2, result.Preferences.RequestedBeerCount);
        Assert.Equal(1000m, result.Preferences.TotalBudget);
        Assert.Contains("mixed order 1 food 2 beers total budget 1000 MKD", result.EffectiveQuery);
    }

    [Fact]
    public void Update_DelegatedChoiceKeepsTheMixedOrderBrief()
    {
        var initial = _service.Update(
            "one food and two beers for 1000 MKD",
            null);
        var followUp = _service.Update("it's on you", initial.Preferences);

        Assert.Equal("mixed", followUp.Preferences.ItemKind);
        Assert.Equal(1000m, followUp.Preferences.TotalBudget);
        Assert.Contains("mixed order", followUp.EffectiveQuery);
    }

    [Fact]
    public void Update_SwitchingAwayFromMixedOrderClearsItsTotalBudget()
    {
        var initial = _service.Update(
            "one food and two beers for 1000 MKD",
            null);
        var beerOnly = _service.Update("beer instead", initial.Preferences);

        Assert.Equal("beer", beerOnly.Preferences.ItemKind);
        Assert.Null(beerOnly.Preferences.RequestedFoodCount);
        Assert.Null(beerOnly.Preferences.RequestedBeerCount);
        Assert.Null(beerOnly.Preferences.TotalBudget);
        Assert.DoesNotContain("total budget", beerOnly.EffectiveQuery);
    }

    [Theory]
    [InlineData("no food, just beer", "beer")]
    [InlineData("beer, not food", "beer")]
    [InlineData("no beer, just food", "food")]
    [InlineData("food, not beer", "food")]
    public void Update_ExplicitNegationWinsOverKeywordPresence(
        string message,
        string expectedKind)
    {
        var result = _service.Update(message, null);

        Assert.Equal(expectedKind, result.Preferences.ItemKind);
        Assert.DoesNotContain("mixed order", result.EffectiveQuery);
    }

    [Fact]
    public void Update_MixedClausesDoNotApplyLightBeerToFoodRichness()
    {
        var result = _service.Update(
            "one spicy food and two light beers for 1000 MKD",
            null);

        Assert.Equal("mixed", result.Preferences.ItemKind);
        Assert.Equal("high", result.Preferences.Heat);
        Assert.Null(result.Preferences.Richness);
        Assert.Equal("low", result.Preferences.Strength);
        Assert.Equal(2, result.Preferences.RequestedBeerCount);
    }

    [Fact]
    public void Update_ExplicitDrinkLanguageChoosesBeer()
    {
        var result = _service.Update("something refreshing to drink", null);

        Assert.Equal("beer", result.Preferences.ItemKind);
        Assert.Contains("refreshing", result.EffectiveQuery);
    }

    [Theory]
    [InlineData("both")]
    [InlineData("one of each")]
    [InlineData("beer and food")]
    [InlineData("food and beer")]
    public void Update_BothMenuSidesCreatesAMixedOrder(string message)
    {
        var result = _service.Update(message, null);

        Assert.Equal("mixed", result.Preferences.ItemKind);
        Assert.Equal(1, result.Preferences.RequestedFoodCount);
        Assert.Equal(1, result.Preferences.RequestedBeerCount);
        Assert.Contains("mixed order", result.EffectiveQuery);
    }

    [Fact]
    public void Update_MixedStyleQuantitiesUseOneTotalBudgetNotPerItemBounds()
    {
        var result = _service.Update(
            "pick one burger and two IPAs for no more than 1200 MKD",
            null);

        Assert.Equal(1, result.Preferences.RequestedFoodCount);
        Assert.Equal(2, result.Preferences.RequestedBeerCount);
        Assert.Equal(1200m, result.Preferences.TotalBudget);
        Assert.Null(result.Preferences.MinimumPrice);
        Assert.Null(result.Preferences.MaximumPrice);
        Assert.Contains("2 beers", result.EffectiveQuery);
        Assert.DoesNotContain("over 1200", result.EffectiveQuery);
    }

    [Fact]
    public void Update_AnotherRoundKeepsTheMixedOrderBrief()
    {
        var initial = _service.Update("one food and one beer for 800 MKD", null);
        var alternative = _service.Update("show me another round", initial.Preferences);

        Assert.Equal("mixed", alternative.Preferences.ItemKind);
        Assert.Equal(1, alternative.Preferences.RequestedFoodCount);
        Assert.Equal(1, alternative.Preferences.RequestedBeerCount);
        Assert.Equal(800m, alternative.Preferences.TotalBudget);
        Assert.Contains("mixed order", alternative.EffectiveQuery);
        Assert.Contains("other choices", alternative.EffectiveQuery);
    }

    [Fact]
    public void Update_NullBrowserCollectionsAreNormalizedSafely()
    {
        var previous = new BeerChatPreferenceState
        {
            Flavours = null!,
            DietaryNeeds = null!,
            ExcludedStyles = null!
        };

        var result = _service.Update("beer", previous);

        Assert.Empty(result.Preferences.Flavours);
        Assert.Empty(result.Preferences.DietaryNeeds);
        Assert.Empty(result.Preferences.ExcludedStyles);
    }
}
