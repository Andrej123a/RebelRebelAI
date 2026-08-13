using Rebel.Domain.Entities;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerProfileSuggestionServiceTests
{
    private readonly BeerProfileSuggestionService _service = new();

    [Fact]
    public void Suggest_ExtractsExplicitAbvStyleAndFlavours()
    {
        var beer = Beer(
            "Rebel Signal",
            "Red IPA 5.9% ABV with grapefruit, citrus and pine aromas.");

        var suggestions = _service.Suggest(beer);

        Assert.Contains(suggestions, item =>
            item.Field == nameof(Product.AlcoholByVolume) && item.Value == "5.9");
        Assert.Contains(suggestions, item =>
            item.Field == nameof(Product.BeerStyle) && item.Value == "Red IPA");
        Assert.Contains(suggestions, item =>
            item.Field == nameof(Product.FlavorNotes) &&
            item.Value.Contains("grapefruit") &&
            item.Value.Contains("citrus") &&
            item.Value.Contains("pine"));
    }

    [Fact]
    public void Suggest_DoesNotReplaceExistingProfileValues()
    {
        var beer = Beer("Known Beer", "IPA 6% with grapefruit.");
        beer.BeerStyle = "Session IPA";
        beer.AlcoholByVolume = 4.8m;
        beer.FlavorNotes = "orange";

        var suggestions = _service.Suggest(beer);

        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.BeerStyle));
        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.AlcoholByVolume));
        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.FlavorNotes));
    }

    [Fact]
    public void Suggest_RequiresPairingCueBeforeSuggestingFood()
    {
        var mentionOnly = Beer("Burger Beer", "Brewed beside our burger station.");
        var explicitPairing = Beer("Dinner Beer", "Pairs well with burgers and spicy food.");

        var mentionSuggestions = _service.Suggest(mentionOnly);
        var pairingSuggestions = _service.Suggest(explicitPairing);

        Assert.DoesNotContain(mentionSuggestions, item => item.Field == nameof(Product.PairingTags));
        Assert.Contains(pairingSuggestions, item =>
            item.Field == nameof(Product.PairingTags) &&
            item.Value.Contains("burger") &&
            item.Value.Contains("spicy food"));
    }

    [Fact]
    public void Suggest_DoesNotGuessSubjectiveScales()
    {
        var beer = Beer("Dark One", "Imperial stout with coffee and chocolate.");

        var suggestions = _service.Suggest(beer);

        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.BodyLevel));
        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.BitternessLevel));
        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.SweetnessLevel));
        Assert.DoesNotContain(suggestions, item => item.Field == nameof(Product.AcidityLevel));
    }

    [Fact]
    public void Suggest_ExtractsCountryOnlyWhenWrittenInMenuText()
    {
        var explicitCountry = Beer("House Wheat", "German wheat beer with banana.");
        var unknownBrand = Beer("Mysterious Brewery Lager", "Clean lager.");

        Assert.Contains(_service.Suggest(explicitCountry), item =>
            item.Field == nameof(Product.OriginCountry) && item.Value == "Germany");
        Assert.DoesNotContain(_service.Suggest(unknownBrand), item =>
            item.Field == nameof(Product.OriginCountry));
    }

    [Fact]
    public void CompletionPercent_UsesActualNormalAndSourCheckCounts()
    {
        var complete = CompleteBeer("Lager");
        var sour = CompleteBeer("Fruit sour");
        sour.AcidityLevel = null;

        Assert.Equal(100, BeerProfileQuality.CompletionPercent(complete));
        Assert.Equal(89, BeerProfileQuality.CompletionPercent(sour));
    }

    private static Product Beer(string name, string description) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Description = description,
        IsAvailable = true
    };

    private static Product CompleteBeer(string style) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Complete Beer",
        IsAvailable = true,
        OriginCountry = "Germany",
        BeerStyle = style,
        AlcoholByVolume = 5m,
        BodyLevel = 2,
        BitternessLevel = 2,
        SweetnessLevel = 2,
        AcidityLevel = 2,
        FlavorNotes = "clean",
        PairingTags = "pizza"
    };
}
