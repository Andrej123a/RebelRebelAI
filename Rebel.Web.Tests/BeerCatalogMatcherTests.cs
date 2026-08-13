using Rebel.Domain.Entities;
using Rebel.Web.Services;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Rebel.Web.Tests;

public class BeerCatalogMatcherTests
{
    private readonly BeerCatalogMatcher _matcher = new();
    private readonly IReadOnlyList<Product> _beers = BuildCatalogue();

    [Theory]
    [InlineData("I want a citrussy IPA", "Citrus Riot")]
    [InlineData("grapefruit aroma", "Citrus Riot")]
    [InlineData("something hoppy and bitter", "Citrus Riot")]
    [InlineData("show me an IPA", "Citrus Riot")]
    [InlineData("a Czech beer", "Clean Czech")]
    [InlineData("pilsner", "Clean Czech")]
    [InlineData("crisp and light", "Clean Czech")]
    [InlineData("German wheat beer", "Easy Wheat")]
    [InlineData("weissbier", "Easy Wheat")]
    [InlineData("something dark", "Dark Matter")]
    [InlineData("coffee stout", "Dark Matter")]
    [InlineData("smoked meat pairing", "Smoke Porter")]
    [InlineData("a beer with dessert", "Dark Matter")]
    [InlineData("strong Belgian beer", "Golden Tripel")]
    [InlineData("tripel", "Golden Tripel")]
    [InlineData("sour and tart", "Cherry Sour")]
    [InlineData("fruity sour", "Cherry Sour")]
    [InlineData("berry flavour", "Cherry Sour")]
    [InlineData("tropical beer", "Tropical Wave")]
    [InlineData("mango aroma", "Tropical Wave")]
    [InlineData("beer for a burger", "Citrus Riot")]
    [InlineData("not bitter", "Easy Wheat")]
    [InlineData("not sweet", "Citrus Riot")]
    [InlineData("under 5%", "Clean Czech")]
    [InlineData("over 8%", "Golden Tripel")]
    [InlineData("low body and light", "Clean Czech")]
    [InlineData("caramel porter", "Smoke Porter")]
    [InlineData("chocolate notes", "Dark Matter")]
    [InlineData("Belgian honey", "Golden Tripel")]
    [InlineData("lemon wheat", "Easy Wheat")]
    public void Shortlist_RanksExpectedBeerFirst(string query, string expectedName)
    {
        var result = _matcher.Shortlist(query, _beers, 3);
        Assert.NotEmpty(result);
        Assert.Equal(expectedName, result[0].Name);
    }

    [Fact]
    public void Shortlist_ExcludesUnavailableBeer()
    {
        var unavailable = Beer("Sold Out IPA", "IPA", "grapefruit", "Belgium", 6, 3, 5, 1);
        unavailable.IsAvailable = false;
        var result = _matcher.Shortlist("grapefruit IPA", _beers.Append(unavailable).ToList(), 8);
        Assert.DoesNotContain(result, beer => beer.Id == unavailable.Id);
    }

    [Fact]
    public void Shortlist_RespectsLimitAndHasNoDuplicates()
    {
        var result = _matcher.Shortlist("fruity beer", _beers, 2);
        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Select(beer => beer.Id).Distinct().Count());
    }

    [Fact]
    public void Shortlist_UsesFeedbackToBreakCloseMatches()
    {
        var first = Beer("Alpha Lager", "Lager", "clean", "Germany", 5, 2, 2, 1);
        var second = Beer("Zed Lager", "Lager", "clean", "Germany", 5, 2, 2, 1);
        var scores = new Dictionary<Guid, double>
        {
            [first.Id] = -2,
            [second.Id] = 2
        };

        var result = _matcher.Shortlist("lager", [first, second], 2, scores);

        Assert.Equal(second.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_UnderAbvConstraintOnlyReturnsMatchingBeers()
    {
        var result = _matcher.Shortlist(
            "strong bitter beer. Hard constraint: under 5% ABV.",
            _beers,
            8);

        Assert.NotEmpty(result);
        Assert.All(result, beer => Assert.True(beer.AlcoholByVolume < 5m));
    }

    [Fact]
    public void Shortlist_LowBitternessConstraintOnlyReturnsMatchingBeers()
    {
        var result = _matcher.Shortlist(
            "hoppy beer. Hard constraint: low bitterness, not bitter.",
            _beers,
            8);

        Assert.NotEmpty(result);
        Assert.All(result, beer => Assert.True(beer.BitternessLevel <= 2));
    }

    [Theory]
    [InlineData("Session IPA, 4.6% ABV", 4.6)]
    [InlineData("Pale ale - 4,8 ABV", 4.8)]
    [InlineData("ABV: 6.6%", 6.6)]
    public void ProfileQuality_ReadsAbvFromExistingMenuDescriptions(
        string description,
        decimal expected)
    {
        var beer = Beer("Legacy Beer", "Ale", "", "", 5m, 2, 2, 1);
        beer.AlcoholByVolume = null;
        beer.Description = description;

        Assert.Equal(expected, BeerProfileQuality.AlcoholByVolume(beer));
    }

    [Fact]
    public void ProfileQuality_CompleteAvailableBeerIsReady()
    {
        var beer = Beer(
            "Ready Beer", "Pilsner", "crisp", "Czechia",
            4.8m, 2, 2, 1, "pizza");

        Assert.Empty(BeerProfileQuality.MissingFields(beer));
        Assert.Empty(BeerProfileQuality.ReviewNotes(beer));
        Assert.True(BeerProfileQuality.IsReadyForGuide(beer));
    }

    [Fact]
    public void ProfileQuality_InferredAbvNeedsStaffConfirmation()
    {
        var beer = Beer(
            "Legacy Beer", "Lager", "clean", "Germany",
            5m, 2, 2, 1, "fries");
        beer.AlcoholByVolume = null;
        beer.Description = "Clean lager, 4.9% ABV.";

        Assert.DoesNotContain("ABV", BeerProfileQuality.MissingFields(beer));
        Assert.Contains(
            BeerProfileQuality.ReviewNotes(beer),
            note => note.Contains("Confirm the ABV"));
        Assert.False(BeerProfileQuality.IsReadyForGuide(beer));
    }

    [Fact]
    public void ProfileQuality_UnavailableBeerNeedsReview()
    {
        var beer = Beer(
            "Paused Beer", "IPA", "citrus", "Hungary",
            6m, 3, 4, 1, "burger");
        beer.IsAvailable = false;

        Assert.Contains(
            BeerProfileQuality.ReviewNotes(beer),
            note => note.StartsWith("Unavailable"));
        Assert.False(BeerProfileQuality.IsReadyForGuide(beer));
    }

    [Fact]
    public void ProfileQuality_SourBeerRequiresAcidity()
    {
        var beer = Beer(
            "Sharp Beer", "Fruit sour", "cherry", "Belgium",
            5m, 2, 2, 2, "cheese");
        beer.AcidityLevel = null;

        Assert.Contains("acidity", BeerProfileQuality.MissingFields(beer));
        Assert.False(BeerProfileQuality.IsReadyForGuide(beer));
    }

    [Fact]
    public void Shortlist_ExactProductNameWins() =>
        Assert.Equal("Clean Czech", _matcher.Shortlist("Clean Czech", _beers, 3)[0].Name);

    [Fact]
    public void Shortlist_ExplicitStyleDoesNotPadWithOtherStyles()
    {
        var result = _matcher.Shortlist("show me three grapefruit IPAs", _beers, 3);

        Assert.Single(result);
        Assert.Equal("Citrus Riot", result[0].Name);
    }

    [Fact]
    public void BuildEvidenceReason_UsesOnlyStoredProfileFacts()
    {
        var beer = _beers.Single(item => item.Name == "Citrus Riot");
        var reason = _matcher.BuildEvidenceReason(beer, "juicy grapefruit IPA");
        Assert.Contains("West Coast IPA", reason);
        Assert.Contains("grapefruit, citrus, pine", reason);
        Assert.Contains("Hungary", reason);
        Assert.Contains("6.2% ABV", reason);
        Assert.DoesNotContain("juicy", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildEvidenceReason_DoesNotInventRequestedFlavour()
    {
        var beer = _beers.Single(item => item.Name == "Easy Wheat");
        var reason = _matcher.BuildEvidenceReason(beer, "grapefruit wheat beer");
        Assert.DoesNotContain("grapefruit", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lemon, banana", reason);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("recommend something")]
    [InlineData("show me two")]
    public void HasUsefulPreference_RejectsVagueRequests(string query) =>
        Assert.False(_matcher.HasUsefulPreference(query));

    [Fact]
    public void Shortlist_YuzuIsRecognizedAsSpecificFlavour()
    {
        var yuzu = Beer("Yuzu Drop", "Pale ale", "yuzu, lemon, lime", "Hungary", 5m, 2, 2, 1);
        var stout = Beer("Local Stout", "Stout", "coffee, chocolate", "North Macedonia", 5m, 4, 2, 2);

        var result = _matcher.Shortlist("now yuzu flavoured beer", [stout, yuzu], 3);

        Assert.Single(result);
        Assert.Equal(yuzu.Id, result[0].Id);
    }

    [Theory]
    [InlineData("grapefruit")]
    [InlineData("not bitter")]
    [InlineData("under 6%")]
    [InlineData("food pairing for pizza")]
    [InlineData("beer for a sausage")]
    [InlineData("something with chicken wings")]
    [InlineData("German wheat")]
    public void HasUsefulPreference_AcceptsSpecificRequests(string query) =>
        Assert.True(_matcher.HasUsefulPreference(query));

    [Fact]
    public void Shortlist_PriceLimitIsStrictAndNotTreatedAsAbv()
    {
        var affordable = Beer("Budget IPA", "IPA", "citrus", "Hungary", 6.5m, 3, 3, 1);
        affordable.Price = 280m;
        var expensive = Beer("Premium IPA", "IPA", "citrus", "Belgium", 4.5m, 3, 2, 1);
        expensive.Price = 360m;

        var result = _matcher.Shortlist(
            "Show me IPAs under 300 denars",
            [affordable, expensive],
            3);

        Assert.Single(result);
        Assert.Equal(affordable.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_ReturnsNoBeerWhenNothingFitsBudget()
    {
        var beer = Beer("Premium IPA", "IPA", "citrus", "Belgium", 6m, 3, 3, 1);
        beer.Price = 360m;

        var result = _matcher.Shortlist("IPA under 300 MKD", [beer], 3);

        Assert.Empty(result);
    }

    [Fact]
    public void Shortlist_CheapestRanksLowestPriceFirst()
    {
        var cheaper = Beer("Value Lager", "Lager", "clean", "Czechia", 4.5m, 2, 2, 1);
        cheaper.Price = 220m;
        var dearer = Beer("Fancy Lager", "Lager", "clean", "Germany", 4.8m, 2, 2, 1);
        dearer.Price = 340m;

        var result = _matcher.Shortlist("Which lager is cheapest?", [dearer, cheaper], 2);

        Assert.Equal(cheaper.Id, result[0].Id);
    }

    [Theory]
    [InlineData("Give me the most expensive beer in the fridge")]
    [InlineData("What is the priciest beer?")]
    [InlineData("Show me the beer with the highest price")]
    [InlineData("Which beer is costliest?")]
    public void Shortlist_MostExpensiveRanksHighestPriceFirst(string query)
    {
        var budget = Beer("Budget Lager", "Lager", "clean", "Czechia", 5m, 2, 2, 1);
        budget.Price = 230m;
        var premium = Beer("Premium Tripel", "Tripel", "honey", "Belgium", 9m, 4, 2, 3);
        premium.Price = 520m;

        var result = _matcher.Shortlist(query, [budget, premium], 2);

        Assert.Equal(premium.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_MostExpensiveStillExcludesUnavailableBeer()
    {
        var available = Beer("Available Premium", "Tripel", "honey", "Belgium", 9m, 4, 2, 3);
        available.Price = 500m;
        var unavailable = Beer("Unavailable Luxury", "Stout", "coffee", "Germany", 12m, 5, 4, 2);
        unavailable.Price = 900m;
        unavailable.IsAvailable = false;

        var result = _matcher.Shortlist("most expensive beer", [available, unavailable], 1);

        Assert.Single(result);
        Assert.Equal(available.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_MostExpensiveStyleRespectsStyleFirst()
    {
        var lager = Beer("Luxury Lager", "Lager", "clean", "Germany", 6m, 3, 2, 1);
        lager.Price = 600m;
        var valueIpa = Beer("Value IPA", "IPA", "citrus", "Hungary", 6m, 3, 4, 1);
        valueIpa.Price = 300m;
        var premiumIpa = Beer("Premium IPA", "IPA", "grapefruit", "Belgium", 8m, 4, 5, 1);
        premiumIpa.Price = 450m;

        var result = _matcher.Shortlist(
            "most expensive IPA",
            [lager, valueIpa, premiumIpa],
            1);

        Assert.Single(result);
        Assert.Equal(premiumIpa.Id, result[0].Id);
    }

    [Theory]
    [InlineData("most expensive beer")]
    [InlineData("priciest lager")]
    [InlineData("highest-priced IPA")]
    public void HasUsefulPreference_AcceptsExpensiveSuperlatives(string query) =>
        Assert.True(_matcher.HasUsefulPreference(query));

    [Fact]
    public void Shortlist_ImpossibleAbvReturnsNoBeer()
    {
        var result = _matcher.Shortlist("lager under 3% ABV", _beers, 3);

        Assert.Empty(result);
    }

    [Fact]
    public void Shortlist_ImpossibleStyleReturnsNoBeer()
    {
        var result = _matcher.Shortlist(
            "show me a stout",
            [Beer("Only Lager", "Lager", "clean", "Germany", 5m, 2, 2, 1)],
            3);

        Assert.Empty(result);
    }

    [Fact]
    public void Shortlist_ImpossibleFlavourReturnsNoBeer()
    {
        var result = _matcher.Shortlist(
            "grapefruit lager",
            [Beer("Clean Lager", "Lager", "bread, clean", "Germany", 5m, 2, 2, 1)],
            3);

        Assert.Empty(result);
    }

    [Fact]
    public void Shortlist_SpecificFlavourDoesNotBroadenToItsFamily()
    {
        var grapefruit = Beer("Grapefruit IPA", "IPA", "grapefruit, pine", "Hungary", 6m, 3, 4, 1);
        var genericCitrus = Beer("Citrus IPA", "IPA", "citrus, orange", "Hungary", 6m, 3, 4, 1);

        var result = _matcher.Shortlist("grapefruit IPA", [genericCitrus, grapefruit], 3);

        Assert.Single(result);
        Assert.Equal(grapefruit.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_CommonGrapefruitTypoStillRequiresGrapefruit()
    {
        var grapefruit = Beer("Grapefruit IPA", "IPA", "grapefruit, pine", "Hungary", 6m, 3, 4, 1);
        var genericCitrus = Beer("Citrus IPA", "IPA", "citrus, orange", "Hungary", 6m, 3, 4, 1);

        var result = _matcher.Shortlist("grapefrut ipa", [genericCitrus, grapefruit], 3);

        Assert.Single(result);
        Assert.Equal(grapefruit.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_ExplicitOriginDoesNotReturnAnotherCountry()
    {
        var result = _matcher.Shortlist("Czech pilsner", _beers, 3);

        Assert.Single(result);
        Assert.All(result, beer => Assert.Equal("Czechia", beer.OriginCountry));
    }

    [Fact]
    public void Shortlist_ImpossibleOriginAndStyleCombinationReturnsNoBeer()
    {
        var result = _matcher.Shortlist("Belgian pilsner", _beers, 3);

        Assert.Empty(result);
    }

    [Fact]
    public void Shortlist_ExplicitFoodDoesNotReturnUnrecordedPairing()
    {
        var result = _matcher.Shortlist("beer for smoked meat", _beers, 3);

        Assert.Single(result);
        Assert.Equal("Smoke Porter", result[0].Name);
    }

    [Fact]
    public void Shortlist_ImpossibleLowBitternessReturnsNoBeer()
    {
        var result = _matcher.Shortlist(
            "IPA that is not bitter",
            [Beer("Firm IPA", "IPA", "citrus", "Hungary", 6m, 3, 5, 1)],
            3);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData("Katsu Burger Katsu Burger")]
    [InlineData("Chicken Strips  Chicken Strips")]
    [InlineData("Lager Lager")]
    public void ProductValidation_RejectsRepeatedName(string name)
    {
        var product = new Product { Name = name };
        var results = new List<ValidationResult>();

        var isValid = Validator.TryValidateObject(
            product,
            new ValidationContext(product),
            results,
            validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, result =>
            result.MemberNames.Contains(nameof(Product.Name)));
    }

    [Theory]
    [InlineData("Katsu Burger")]
    [InlineData("Rebel Rebel Burger")]
    [InlineData("Lord of the Rings Rohan")]
    public void ProductValidation_AllowsLegitimateName(string name)
    {
        var product = new Product { Name = name };
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(
            product,
            new ValidationContext(product),
            results,
            validateAllProperties: true));
    }

    private static IReadOnlyList<Product> BuildCatalogue() =>
    [
        Beer("Citrus Riot", "West Coast IPA", "grapefruit, citrus, pine", "Hungary", 6.2m, 3, 5, 1, "burger, spicy food", popular: true),
        Beer("Clean Czech", "Czech pilsner", "bread, crisp, clean", "Czechia", 4.5m, 1, 2, 1, "pizza"),
        Beer("Dark Matter", "Imperial stout", "coffee, chocolate, roasted malt", "Germany", 7.5m, 5, 3, 3, "dessert"),
        Beer("Cherry Sour", "Fruit sour", "cherry, berry, tart", "Belgium", 5m, 2, 2, 2, "spicy food, cheese", acidity: 5),
        Beer("Golden Tripel", "Belgian tripel", "honey, banana, spice", "Belgium", 9m, 4, 2, 4, "cheese, dessert"),
        Beer("Easy Wheat", "Wheat beer", "lemon, banana", "Germany", 4.8m, 2, 1, 2, "salad, chicken"),
        Beer("Tropical Wave", "Pale ale", "mango, passionfruit, tropical", "Hungary", 5.5m, 3, 3, 2, "tacos"),
        Beer("Smoke Porter", "Porter", "smoke, caramel, coffee", "Czechia", 6.8m, 4, 3, 3, "smoked meat")
    ];

    private static Product Beer(string name, string style, string flavours, string country,
        decimal abv, int body, int bitterness, int sweetness, string pairings = "",
        int acidity = 1, bool popular = false) => new()
    {
        Id = Guid.NewGuid(), Name = name, BeerStyle = style, FlavorNotes = flavours,
        OriginCountry = country, AlcoholByVolume = abv, BodyLevel = body,
        BitternessLevel = bitterness, SweetnessLevel = sweetness, AcidityLevel = acidity,
        PairingTags = pairings, IsAvailable = true, IsPopular = popular,
        Category = new Category { Name = "Beers" }
    };
}
