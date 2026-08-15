using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class FoodCatalogMatcherTests
{
    private readonly FoodCatalogMatcher _matcher = new();

    [Fact]
    public void Shortlist_HotAndSaltyRanksTheStrongestCombinedProfileFirst()
    {
        var buffalo = Food("Buffalo Wings", heat: 5, saltiness: 4, richness: 4);
        var fries = Food("French Fries", heat: 1, saltiness: 5, richness: 2);

        var result = _matcher.Shortlist(
            "something hot and salty",
            [fries, buffalo],
            2);

        Assert.Equal(buffalo.Id, result[0].Id);
    }

    [Fact]
    public void HasFoodPreference_HotWeatherIsNotSpicyFoodIntent()
    {
        Assert.False(_matcher.HasFoodPreference(
            "I'd like something refreshing these hot days"));
        Assert.True(_matcher.HasFoodPreference(
            "I'd like something hot and spicy to eat"));
    }

    [Fact]
    public void Shortlist_MildRequestExcludesHotFood()
    {
        var mild = Food("Chicken Wings", heat: 1, saltiness: 3, richness: 3);
        var hot = Food("Buffalo Wings", heat: 5, saltiness: 4, richness: 4);

        var result = _matcher.Shortlist("mild food, not spicy", [hot, mild], 3);

        var match = Assert.Single(result);
        Assert.Equal(mild.Id, match.Id);
    }

    [Fact]
    public void Shortlist_ExplicitCategoryReturnsOnlyThatFoodType()
    {
        var pizza = Food("Pepperoni Pizza", 3, 5, 5, "Pizza");
        var burger = Food("Smash Burger", 3, 4, 5, "Burgers");

        var result = _matcher.Shortlist("show me spicy pizza", [burger, pizza], 3);

        var match = Assert.Single(result);
        Assert.Equal(pizza.Id, match.Id);
    }

    [Fact]
    public void Shortlist_RespectsVeganAndAvailabilityConstraints()
    {
        var vegan = Food("Vegan Burger", 1, 3, 3, "Burgers");
        vegan.IsVegan = true;
        var unavailableVegan = Food("Vegan Wings", 4, 4, 3, "Wings");
        unavailableVegan.IsVegan = true;
        unavailableVegan.IsAvailable = false;
        var beef = Food("Rebel Burger", 1, 4, 5, "Burgers");

        var result = _matcher.Shortlist(
            "recommend vegan food",
            [beef, unavailableVegan, vegan],
            3);

        var match = Assert.Single(result);
        Assert.Equal(vegan.Id, match.Id);
    }

    [Fact]
    public void Shortlist_AroundPriceRanksNearestFoodFirst()
    {
        var wings = Food("Buffalo Wings", 5, 4, 4, "Wings");
        wings.Price = 290m;
        var fries = Food("French Fries", 1, 5, 2, "Fries");
        fries.Price = 190m;
        var pizza = Food("Pepperoni Pizza", 3, 5, 5, "Pizza");
        pizza.Price = 390m;

        var result = _matcher.Shortlist(
            "three foods around 300 MKD",
            [pizza, fries, wings],
            3);

        Assert.Equal(wings.Id, result[0].Id);
    }

    [Fact]
    public void Shortlist_FoodPriceRangeTierAndSuperlativeAreApplied()
    {
        var budget = Food("Fries", 1, 5, 2);
        budget.Price = 190m;
        var middle = Food("Wings", 4, 4, 4);
        middle.Price = 290m;
        var premium = Food("Big Plate", 2, 4, 5);
        premium.Price = 490m;

        var range = _matcher.Shortlist(
            "food between 250 and 400 MKD",
            [premium, budget, middle],
            3);
        var tier = _matcher.Shortlist("$$ food", [premium, budget, middle], 3);
        var cheapest = _matcher.Shortlist("cheapest food", [premium, budget, middle], 1);

        Assert.Single(range);
        Assert.Equal(middle.Id, range[0].Id);
        Assert.Single(tier);
        Assert.Equal(middle.Id, tier[0].Id);
        Assert.Equal(budget.Id, cheapest[0].Id);
    }

    [Fact]
    public async Task Chat_AmbiguousPriceAsksBeerOrFood()
    {
        var beer = Beer("Citrus Riot");
        beer.Price = 320m;
        var food = Food("Buffalo Wings", 5, 4, 4, "Wings");
        food.Price = 290m;
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "something around 300 MKD",
            "around 300 MKD",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, food]);

        Assert.Empty(result.Matches);
        Assert.Contains("beer or food", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, result.FollowUps?.Count);
    }

    [Fact]
    public async Task Chat_ExplicitFoodAroundPriceReturnsNearestDishAndPrice()
    {
        var beer = Beer("Citrus Riot");
        var wings = Food("Buffalo Wings", 5, 4, 4, "Wings");
        wings.Price = 290m;
        var pizza = Food("Pepperoni Pizza", 3, 5, 5, "Pizza");
        pizza.Price = 390m;
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "food around 300 MKD",
            "food around 300 MKD",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, pizza, wings]);

        Assert.Equal(wings.Id, result.Matches[0].Beer.Id);
        Assert.Contains("300 MKD", result.Reply);
        Assert.Contains("290 MKD", result.Reply);
    }

    [Fact]
    public async Task Chat_HotAndSaltyRequestReturnsFoodInsteadOfBeer()
    {
        var beer = Beer("Citrus Riot");
        var buffalo = Food("Buffalo Wings", 5, 4, 4, "Wings");
        buffalo.FlavorNotes = "chicken, buffalo sauce, ranch";
        var fries = Food("French Fries", 1, 5, 2, "Fries");
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "I want something hot and salty",
            "hot salty",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, fries, buffalo]);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Food, match.Beer.Category!.Type));
        Assert.Equal(buffalo.Id, result.Matches[0].Beer.Id);
        Assert.Contains("Buffalo Wings", result.Reply);
    }

    [Fact]
    public async Task Chat_NaturalMildFoodRequestIsNotTreatedAsAProductName()
    {
        var beer = Beer("Citrus Riot");
        var mild = Food("Chicken Wings", 1, 3, 3, "Wings");
        var hot = Food("Buffalo Wings", 5, 4, 4, "Wings");
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            "Give me two mild foods, nothing spicy",
            "mild food not spicy",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, mild, hot]);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match => Assert.True(match.Beer.HeatLevel <= 2));
        Assert.DoesNotContain("regular menu", result.Reply);
    }

    [Theory]
    [InlineData("I need a beer pairing for Buffalo Wings")]
    [InlineData("What beer should I have with a Rebel Burger? Give me two options.")]
    public async Task Chat_BeerPairingForFoodStillReturnsBeer(string message)
    {
        var beer = Beer("Citrus Riot");
        beer.PairingTags = "buffalo wings, spicy food";
        var buffalo = Food("Buffalo Wings", 5, 4, 4, "Wings");
        var service = CreateService();

        var result = await service.ReplyStructuredAsync(
            message,
            "beer pairing buffalo wings",
            [beer],
            new Dictionary<Guid, double>(),
            CancellationToken.None,
            [beer, buffalo]);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
    }

    private static OpenAiBeerGuideChatService CreateService() =>
        new(
            new ConfigurationBuilder().Build(),
            new BeerCatalogMatcher(),
            NullLogger<OpenAiBeerGuideChatService>.Instance,
            foodMatcher: new FoodCatalogMatcher());

    private static Product Food(
        string name,
        int heat,
        int saltiness,
        int richness,
        string category = "Food") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        HeatLevel = heat,
        SaltinessLevel = saltiness,
        RichnessLevel = richness,
        SweetnessLevel = 2,
        AcidityLevel = 2,
        FlavorNotes = name,
        IsAvailable = true,
        Category = new Category { Name = category, Type = CategoryType.Food }
    };

    private static Product Beer(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BeerStyle = "IPA",
        FlavorNotes = "citrus, hops",
        IsAvailable = true,
        Category = new Category { Name = "Beer", Type = CategoryType.Beer }
    };
}
