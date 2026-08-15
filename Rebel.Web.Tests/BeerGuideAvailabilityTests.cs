using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Models;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerGuideAvailabilityTests
{
    [Fact]
    public async Task Reply_NeverReturnsUnavailableBeerAsRecommendation()
    {
        var unavailable = Beer("Sold Out Citrus IPA", false);
        var available = Beer("Backup Citrus IPA", true);
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Sold Out Citrus IPA",
            [],
            [unavailable, available],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match => Assert.True(match.Beer.IsAvailable));
        Assert.DoesNotContain(result.Matches, match => match.Beer.Id == unavailable.Id);
        Assert.Contains("currently unavailable", result.Reply);
        Assert.Contains("available alternative", result.Reply);
    }

    [Fact]
    public async Task Reply_WhenEveryMatchIsUnavailable_ReturnsNoBeerCards()
    {
        var unavailable = Beer("Sold Out Citrus IPA", false);
        var service = CreateService();

        var result = await service.ReplyAsync(
            "citrus IPA",
            [],
            [unavailable],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Empty(result.Matches);
        Assert.Contains("currently unavailable", result.Reply);
        Assert.Contains("do not have an available alternative", result.Reply);
    }

    [Fact]
    public async Task Reply_DirectUnavailableBeerLookupReportsTemporaryStockState()
    {
        var soldOut = Beer("Sold Out Citrus IPA", false);
        var alternative = Beer("Backup Citrus IPA", true);
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Do you have Sold Out Citrus IPA?",
            "Sold Out Citrus IPA",
            [soldOut, alternative],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [soldOut, alternative]);

        Assert.Empty(result.Matches);
        Assert.Contains("temporarily out of stock", result.Reply);
        Assert.Contains(soldOut.Name, result.Reply);
        Assert.DoesNotContain("alternative", result.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reply_UnknownBeerLookupSaysItIsNotOnRegularMenu()
    {
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Do you have Corona?",
            "Corona",
            [Beer("Citrus Riot", true)],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [Beer("Citrus Riot", true)]);

        Assert.Empty(result.Matches);
        Assert.Contains("don't have Corona", result.Reply);
        Assert.Contains("regular menu", result.Reply);
    }

    [Fact]
    public async Task Reply_UnknownFoodLookupDoesNotReturnRandomBeer()
    {
        var beer = Beer("Citrus Riot", true);
        var burger = Food("Katsu Burger", true);
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Show me sushi",
            "sushi",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, burger]);

        Assert.Empty(result.Matches);
        Assert.Contains("don't have sushi", result.Reply);
        Assert.Contains("regular menu", result.Reply);
        Assert.DoesNotContain(beer.Name, result.Reply);
    }

    [Fact]
    public async Task Reply_AvailableFoodLookupConfirmsCurrentAvailability()
    {
        var beer = Beer("Citrus Riot", true);
        var burger = Food("Katsu Burger", true);
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Is Katsu Burger available?",
            "Katsu Burger",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, burger]);

        Assert.Empty(result.Matches);
        Assert.Contains("Katsu Burger", result.Reply);
        Assert.Contains("available right now", result.Reply);
    }

    [Fact]
    public async Task Reply_GenericBeerPreferenceStillUsesRecommendations()
    {
        var beer = Beer("Citrus Riot", true);
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "I want a citrussy IPA",
            "citrussy IPA",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer]);

        Assert.NotEmpty(result.Matches);
        Assert.DoesNotContain("regular menu", result.Reply);
    }

    [Fact]
    public async Task Reply_YuzuBeerReturnsTokyoLemonadeInsteadOfMissingMenuMessage()
    {
        var tokyo = BeerWithProfile(
            "Tokyo Lemonade 0.44L",
            "Yuzu witbier",
            "yuzu, orange peel, coriander, wheat");
        var citrusIpa = BeerWithProfile(
            "Citrus Riot",
            "IPA",
            "grapefruit, lemon, pine");
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "I want a yuzu beer",
            "yuzu beer",
            [citrusIpa, tokyo],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [citrusIpa, tokyo]);

        var match = Assert.Single(result.Matches);
        Assert.Equal(tokyo.Id, match.Beer.Id);
        Assert.Contains("Yuzu?", result.Reply);
        Assert.Contains("Tokyo Lemonade", result.Reply);
        Assert.DoesNotContain("regular menu", result.Reply);
        Assert.False(result.UsedAi);
    }

    [Fact]
    public void Matcher_ExcludesUnavailableByDefault()
    {
        var matcher = new BeerCatalogMatcher();
        var unavailable = Beer("Sold Out Citrus IPA", false);
        var available = Beer("Backup Citrus IPA", true);

        var result = matcher.Shortlist(
            "Sold Out Citrus IPA",
            [unavailable, available],
            3);

        Assert.Single(result);
        Assert.Equal(available.Id, result[0].Id);
    }

    [Fact]
    public async Task Reply_AcceptsExactCatalogueNameAsUsefulContext()
    {
        var namedBeer = Beer("Midnight Engine", true);
        namedBeer.BeerStyle = "Experimental";
        namedBeer.FlavorNotes = null;
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Tell me about Midnight Engine and show me two similar available beers.",
            [],
            [namedBeer],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal(namedBeer.Id, result.Matches[0].Beer.Id);
        Assert.DoesNotContain("Give me one clue", result.Reply);
    }

    [Fact]
    public async Task Reply_NamedBeerExplainsProfileThenAddsRequestedAlternativeCount()
    {
        var galaxy = BeerWithProfile(
            "Galaxy Lager 0.33L",
            "New-wave pilsner",
            "crisp malt, citrus, tropical fruit, Galaxy hops");
        galaxy.OriginCountry = "Hungary";
        galaxy.AlcoholByVolume = 5m;
        var similarOne = BeerWithProfile(
            "Citrus Pils",
            "Pilsner",
            "crisp malt, citrus hops");
        var similarTwo = BeerWithProfile(
            "Tropical Lager",
            "Lager",
            "tropical fruit, clean malt");
        var unrelated = BeerWithProfile(
            "Coffee Stout",
            "Stout",
            "coffee, chocolate");
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Tell me about Galaxy Lager 0.33L and show me two similar available beers.",
            [],
            [galaxy, similarOne, similarTwo, unrelated],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(3, result.Matches.Count);
        Assert.Equal(galaxy.Id, result.Matches[0].Beer.Id);
        Assert.Contains("New-wave pilsner", result.Reply);
        Assert.Contains("Hungary", result.Reply);
        Assert.Contains("5% ABV", result.Reply);
        Assert.Contains("aromas and flavours", result.Reply);
        Assert.Contains("crisp malt", result.Reply);
        Assert.Contains("2 similar available beers", result.Reply);
        Assert.DoesNotContain(result.Matches, match => match.Beer.Id == unrelated.Id);
        Assert.False(result.UsedAi);
    }

    [Fact]
    public async Task Reply_StructuredCountDoesNotHideNamedBeerWithoutStyleInItsName()
    {
        var namedBeer = BeerWithProfile(
            "Liquid Cocaine 0.44L",
            "Double IPA",
            "resinous hops, citrus peel, caramel malt");
        var alternativeOne = BeerWithProfile(
            "Hop Riot",
            "Double IPA",
            "resinous hops, citrus peel");
        var alternativeTwo = BeerWithProfile(
            "Malt Voltage",
            "Strong ale",
            "caramel malt, warming finish");
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Tell me about Liquid Cocaine 0.44L and show me two similar available beers.",
            "show me 2",
            [namedBeer, alternativeOne, alternativeTwo],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(3, result.Matches.Count);
        Assert.Equal(namedBeer.Id, result.Matches[0].Beer.Id);
        Assert.Contains("Double IPA", result.Reply);
        Assert.Contains("resinous hops", result.Reply);
        Assert.Contains("2 similar available beers", result.Reply);
        Assert.DoesNotContain("Give me one clue", result.Reply);
    }

    [Fact]
    public async Task Reply_SimilarBeerFollowUpReturnsOnlyProfileBasedAlternatives()
    {
        var source = BeerWithProfile(
            "Liquid Cocaine 0.44L",
            "West Coast double IPA",
            "citrus, ripe fruit, pine, resin, complex malt");
        var closeOne = BeerWithProfile(
            "Jam 72",
            "West Coast IPA",
            "citrus, tropical fruit, pine, resin, dry finish");
        var closeTwo = BeerWithProfile(
            "Blue Pill",
            "Tea-infused West Coast IPA",
            "citrus, pine, resin, black tea, dry finish");
        var unrelated = BeerWithProfile(
            "Tokyo Lemonade",
            "Yuzu witbier",
            "yuzu, orange peel, coriander, wheat");
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Show me two available beers similar to Liquid Cocaine 0.44L.",
            "show me 2",
            [source, closeOne, closeTwo, unrelated],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(2, result.Matches.Count);
        Assert.DoesNotContain(result.Matches, match => match.Beer.Id == source.Id);
        Assert.Contains(result.Matches, match => match.Beer.Id == closeOne.Id);
        Assert.Contains(result.Matches, match => match.Beer.Id == closeTwo.Id);
        Assert.DoesNotContain(result.Matches, match => match.Beer.Id == unrelated.Id);
        Assert.Contains("similar profile to Liquid Cocaine", result.Reply);
    }

    [Theory]
    [InlineData("Explain me the taste profile of Tokyo Lemonade")]
    [InlineData("How does Tokyo Lemonade taste?")]
    [InlineData("What aromas does Tokyo Lemonade have?")]
    [InlineData("Where is Tokyo Lemonade from?")]
    public async Task Reply_NaturalNamedBeerQuestionsReturnItsProfile(string question)
    {
        var beer = BeerWithProfile(
            "Tokyo Lemonade 0.44L",
            "Japanese rice lager",
            "lemon zest, crisp rice malt, floral hops");
        beer.OriginCountry = "Hungary";
        beer.AlcoholByVolume = 4.5m;
        var service = CreateService();

        var result = await service.ReplyAsync(
            question,
            [],
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal(beer.Id, result.Matches[0].Beer.Id);
        Assert.Contains("Japanese rice lager", result.Reply);
        Assert.Contains("Hungary", result.Reply);
        Assert.Contains("lemon zest", result.Reply);
        Assert.Contains("Would you like me to suggest", result.Reply);
        var followUp = Assert.Single(result.FollowUps!);
        Assert.Equal("Show similar beers", followUp.Label);
        Assert.Contains("Tokyo Lemonade 0.44L", followUp.Prompt);
        Assert.DoesNotContain("Give me one clue", result.Reply);
        Assert.False(result.UsedAi);
    }

    [Fact]
    public async Task Reply_UnavailableNamedBeerIsExplainedButNotRecommended()
    {
        var soldOut = BeerWithProfile(
            "Sold Out Lager",
            "Lager",
            "crisp malt, herbs");
        soldOut.IsAvailable = false;
        var alternative = BeerWithProfile(
            "Available Lager",
            "Lager",
            "crisp malt, herbs");
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Tell me about Sold Out Lager and show me one similar beer.",
            [],
            [soldOut, alternative],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal(alternative.Id, result.Matches[0].Beer.Id);
        Assert.Contains("currently unavailable", result.Reply);
        Assert.Contains("one similar available beer", result.Reply);
    }

    [Fact]
    public async Task Reply_RefinesEarlierRequestWithLowerBitterness()
    {
        var bitter = Beer("Bitter Grapefruit", true);
        bitter.BitternessLevel = 5;
        var gentler = Beer("Gentle Grapefruit", true);
        gentler.BitternessLevel = 2;
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Something less bitter.",
            [new BeerChatTurn { Role = "user", Text = "I want a grapefruit IPA." }],
            [bitter, gentler],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match => Assert.True(match.Beer.BitternessLevel <= 2));
        Assert.Equal(gentler.Id, result.Matches[0].Beer.Id);
    }

    [Fact]
    public async Task Reply_ImpossibleRequestReturnsRecoveryWithoutBeerCards()
    {
        var beer = Beer("Firm Citrus IPA", true);
        beer.Price = 350m;
        var service = CreateService();

        var result = await service.ReplyAsync(
            "IPA under 200 denars",
            [],
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Empty(result.Matches);
        Assert.False(result.UsedAi);
        Assert.NotEmpty(result.FollowUps!);
        Assert.Contains("budget", result.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reply_WhenRequestedSourIsUnavailableNamesItWithoutRecommendingIt()
    {
        var unavailableSour = Beer("Sold Out Cherry Sour", false);
        unavailableSour.BeerStyle = "Fruit sour";
        unavailableSour.FlavorNotes = "cherry, berry, tart";
        unavailableSour.AcidityLevel = 5;
        var availableLager = Beer("Clean Lager", true);
        availableLager.BeerStyle = "Lager";
        availableLager.FlavorNotes = "bread, herbs";
        var service = CreateService();

        var result = await service.ReplyAsync(
            "sour beer",
            [],
            [unavailableSour, availableLager],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Empty(result.Matches);
        Assert.Contains(unavailableSour.Name, result.Reply);
        Assert.Contains("currently unavailable", result.Reply);
        Assert.Contains("do not have a sour beer available", result.Reply,
            StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(result.FollowUps!);
    }

    [Theory]
    [InlineData("sour?")]
    [InlineData("sour beer")]
    public async Task Reply_BroadStyleRequestReturnsEveryAvailableMatchingBeer(string query)
    {
        var sours = Enumerable.Range(1, 9)
            .Select(index =>
            {
                var beer = Beer($"Sour {index}", true);
                beer.BeerStyle = "Fruit sour";
                beer.FlavorNotes = "berry, tart citrus";
                beer.AcidityLevel = 4;
                return beer;
            })
            .ToList();
        var lager = Beer("Clean Lager", true);
        lager.BeerStyle = "Lager";
        lager.FlavorNotes = "bread, herbs";
        var service = CreateService();

        var result = await service.ReplyAsync(
            query,
            [],
            [.. sours, lager],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(9, result.Matches.Count);
        Assert.All(result.Matches, match =>
            Assert.Contains("sour", match.Beer.BeerStyle!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reply_ExplicitStyleCountStillLimitsTheAnswer()
    {
        var sours = Enumerable.Range(1, 9)
            .Select(index =>
            {
                var beer = Beer($"Sour {index}", true);
                beer.BeerStyle = "Fruit sour";
                beer.AcidityLevel = 4;
                return beer;
            })
            .ToList();
        var service = CreateService();

        var result = await service.ReplyAsync(
            "show me three sour beers",
            [],
            sours,
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(3, result.Matches.Count);
    }

    [Fact]
    public async Task Reply_BroadHungarianRequestAsksForTasteBeforeShowingCards()
    {
        var hungarianBeers = new[]
        {
            BeerWithProfile("Clean Pils", "Pilsner", "crisp, clean"),
            BeerWithProfile("Citrus Riot", "IPA", "grapefruit, citrus"),
            BeerWithProfile("Dark Engine", "Stout", "coffee, chocolate"),
            BeerWithProfile("Cherry Noise", "Fruit sour", "cherry, tart")
        };
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Hungarian beer",
            [],
            hungarianBeers,
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Empty(result.Matches);
        Assert.Contains("Plenty", result.Reply);
        Assert.Contains("4 Hungarian beers", result.Reply);
        Assert.Contains("What kind are you in the mood for", result.Reply);
        Assert.Contains(result.FollowUps!, item => item.Label == "Crisp & easy");
        Assert.Contains(result.FollowUps!, item => item.Label == "Hoppy & citrusy");
        Assert.Contains(result.FollowUps!, item => item.Label == "Dark & roasty");
        Assert.Contains(result.FollowUps!, item => item.Label == "Sour & fruity");
        Assert.All(result.FollowUps!, item =>
            Assert.Contains("Hungarian", item.Prompt, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reply_SpecificHungarianTasteRequestReturnsBeerInsteadOfClarifyingAgain()
    {
        var hungarianBeers = new[]
        {
            BeerWithProfile("Clean Pils", "Pilsner", "crisp, clean"),
            BeerWithProfile("Citrus Riot", "IPA", "grapefruit, citrus"),
            BeerWithProfile("Dark Engine", "Stout", "coffee, chocolate"),
            BeerWithProfile("Cherry Noise", "Fruit sour", "cherry, tart")
        };
        var service = CreateService();

        var result = await service.ReplyAsync(
            "Show me hoppy and citrusy Hungarian beers.",
            [],
            hungarianBeers,
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.NotEmpty(result.Matches);
        Assert.Contains(result.Matches, match => match.Beer.Name == "Citrus Riot");
    }

    [Fact]
    public async Task Reply_SingleMacedonianResultSoundsLikeAContextualBartenderPick()
    {
        var local = BeerWithProfile(
            "BAK Stout 0.5L",
            "Stout",
            "roasted barley, coffee, dark chocolate, caramel");
        local.OriginCountry = "North Macedonia";
        var imported = BeerWithProfile("Imported Lager", "Lager", "crisp malt");
        imported.OriginCountry = "Germany";
        var service = CreateService();

        var result = await service.ReplyAsync(
            "now, give me some macedonian beers",
            [],
            [local, imported],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        var match = Assert.Single(result.Matches);
        Assert.Equal(local.Id, match.Beer.Id);
        Assert.Contains("BAK Stout 0.5L is my Macedonian pick tonight", result.Reply);
        Assert.Contains("only Macedonian beer available", result.Reply);
        Assert.Contains("roasted barley", result.Reply);
        Assert.DoesNotContain("This is where I would start", result.Reply);
    }

    [Fact]
    public async Task Reply_SingleStyleResultAcknowledgesTheRequestedStyle()
    {
        var stout = BeerWithProfile("Night Shift", "Stout", "coffee, cocoa");
        var service = CreateService();

        var result = await service.ReplyAsync(
            "give me a stout",
            [],
            [stout],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Contains("For a stout, I would hand you Night Shift", result.Reply);
        Assert.Contains("coffee, cocoa", result.Reply);
    }

    [Fact]
    public async Task Reply_MultipleStyleResultsUseConversationalPluralWording()
    {
        var service = CreateService();
        var result = await service.ReplyAsync(
            "now give me a stout",
            [],
            [
                BeerWithProfile("Night Shift", "Stout", "coffee, cocoa"),
                BeerWithProfile("Dark Engine", "Oatmeal stout", "roast, caramel"),
                BeerWithProfile("Velvet Noise", "Milk stout", "chocolate, vanilla")
            ],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Contains("In a stout mood?", result.Reply);
        Assert.Contains("three worth your time", result.Reply);
        Assert.DoesNotContain("these 3 make the cut", result.Reply);
    }

    [Fact]
    public async Task Reply_GreetingFeelsConversationalWithoutShowingBeerCards()
    {
        var service = CreateService();

        var result = await service.ReplyAsync(
            "hey",
            [],
            [Beer("Citrus Riot", true)],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Empty(result.Matches);
        Assert.StartsWith("Hey.", result.Reply);
        Assert.Contains("mood", result.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reply_SurpriseMeReturnsPopularAvailableBeersWithoutCallingAi()
    {
        var popular = Beer("House Favourite", true);
        popular.IsPopular = true;
        var regular = Beer("Good Backup", true);
        var unavailable = Beer("Sold Out Star", false);
        unavailable.IsPopular = true;
        var service = CreateService();

        var result = await service.ReplyAsync(
            "surprise me",
            [],
            [regular, unavailable, popular],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.NotEmpty(result.Matches);
        Assert.Equal(popular.Id, result.Matches[0].Beer.Id);
        Assert.DoesNotContain(result.Matches, match => !match.Beer.IsAvailable);
        Assert.False(result.UsedAi);
    }

    [Fact]
    public async Task Reply_HighestAlcoholReturnsExactTopFourByAbvAfterSourHistory()
    {
        var beers = new[]
        {
            BeerAtAbv("Beer 4", 4m),
            BeerAtAbv("Beer 6", 6m),
            BeerAtAbv("Beer 8", 8m),
            BeerAtAbv("Beer 9", 9m),
            BeerAtAbv("Beer 10", 10m),
            BeerAtAbv("Beer 12", 12m)
        };
        var service = CreateService();

        var result = await service.ReplyAsync(
            "let's drink some high %abv beers. recommend me 4 of the highest alcohol beers you have",
            [new BeerChatTurn { Role = "user", Text = "sour beer" }],
            beers,
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(4, result.Matches.Count);
        Assert.Equal(
            new[] { 12m, 10m, 9m, 8m },
            result.Matches.Select(match => match.Beer.AlcoholByVolume!.Value));
        Assert.DoesNotContain("sour", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.False(result.UsedAi);
    }

    [Fact]
    public async Task Reply_NotSourHighestAlcoholActivelyExcludesSourStyles()
    {
        var strongestSour = BeerAtAbv("Strong Sour", 14m);
        strongestSour.BeerStyle = "Imperial sour";
        var beers = new[]
        {
            strongestSour,
            BeerAtAbv("Strong Ale", 12m),
            BeerAtAbv("Tripel", 10m),
            BeerAtAbv("Double IPA", 9m),
            BeerAtAbv("Porter", 8m)
        };
        var service = CreateService();

        var result = await service.ReplyAsync(
            "no, not sour, i want high %abv beer now. show me 4 of the highest alcohol beers",
            [new BeerChatTurn { Role = "user", Text = "sour beer" }],
            beers,
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Equal(4, result.Matches.Count);
        Assert.DoesNotContain(result.Matches, match => match.Beer.Id == strongestSour.Id);
        Assert.All(result.Matches, match =>
            Assert.DoesNotContain("sour", match.Beer.BeerStyle!, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reply_MostExpensiveReturnsOneHighestPricedAvailableBeer()
    {
        var cheaper = Beer("Veltins Pilsner", true);
        cheaper.Price = 230m;
        var premium = Beer("Premium Tripel", true);
        premium.Price = 520m;
        premium.BeerStyle = "Tripel";
        var unavailableLuxury = Beer("Unavailable Luxury", false);
        unavailableLuxury.Price = 900m;
        var service = CreateService();

        var result = await service.ReplyAsync(
            "give me the most expensive beer in the fridge",
            [],
            [cheaper, premium, unavailableLuxury],
            new Dictionary<Guid, double>(),
            CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal(premium.Id, result.Matches[0].Beer.Id);
        Assert.Equal(520m, result.Matches[0].Beer.Price);
    }

    private static OpenAiBeerGuideChatService CreateService() =>
        new(
            new ConfigurationBuilder().Build(),
            new BeerCatalogMatcher(),
            NullLogger<OpenAiBeerGuideChatService>.Instance);

    private static Product Beer(string name, bool isAvailable) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BeerStyle = "IPA",
        FlavorNotes = "citrus, grapefruit",
        OriginCountry = "Hungary",
        AlcoholByVolume = 6.2m,
        BodyLevel = 3,
        BitternessLevel = 4,
        SweetnessLevel = 1,
        PairingTags = "burger",
        IsAvailable = isAvailable,
        Category = new Category { Name = "Beer", Type = CategoryType.Beer }
    };

    private static Product Food(string name, bool isAvailable) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsAvailable = isAvailable,
        Category = new Category { Name = "Food", Type = CategoryType.Food }
    };

    private static Product BeerWithProfile(string name, string style, string flavours)
    {
        var beer = Beer(name, true);
        beer.BeerStyle = style;
        beer.FlavorNotes = flavours;
        return beer;
    }

    private static Product BeerAtAbv(string name, decimal abv)
    {
        var beer = Beer(name, true);
        beer.AlcoholByVolume = abv;
        beer.BeerStyle = "Ale";
        beer.FlavorNotes = "malt";
        return beer;
    }
}
