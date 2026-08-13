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
}
