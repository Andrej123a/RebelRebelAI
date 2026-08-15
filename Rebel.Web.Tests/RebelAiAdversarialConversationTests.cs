using Rebel.Domain.Enums;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class RebelAiAdversarialConversationTests
{
    [Theory]
    [InlineData("what would you recommend?")]
    [InlineData("what do you suggest?")]
    [InlineData("can you suggest something?")]
    [InlineData("any recommendations?")]
    [InlineData("what should I order?")]
    [InlineData("your best pick?")]
    [InlineData("help me choose")]
    [InlineData("what's good?")]
    public async Task VagueRecommendationLanguage_AsksWhichMenuSide(string message)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.Empty(result.Matches);
        Assert.Contains("beer", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eat", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, result.FollowUps?.Count);
    }

    [Theory]
    [InlineData("recommend me a beer")]
    [InlineData("suggest a beer")]
    [InlineData("something refreshing to drink")]
    [InlineData("show me a stout")]
    [InlineData("give me two IPAs")]
    [InlineData("a yuzu beer please")]
    [InlineData("beer under 400 MKD")]
    [InlineData("lowest alcohol beer")]
    public async Task ExplicitBeerLanguage_NeverReturnsFood(string message)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
    }

    [Theory]
    [InlineData("recommend me food")]
    [InlineData("suggest something to eat")]
    [InlineData("show me spicy food")]
    [InlineData("give me a burger")]
    [InlineData("food under 350 MKD")]
    [InlineData("cheapest dish")]
    [InlineData("something mild to eat")]
    public async Task ExplicitFoodLanguage_NeverReturnsBeer(string message)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Food, match.Beer.Category!.Type));
    }

    [Theory]
    [InlineData("both", 1, 1)]
    [InlineData("one of each", 1, 1)]
    [InlineData("beer and food", 1, 1)]
    [InlineData("food and beer", 1, 1)]
    [InlineData("pick one burger and two IPAs for no more than 1200 MKD", 1, 2)]
    [InlineData("one spicy food and two light beers for 1000 MKD", 1, 2)]
    public async Task MixedLanguage_ReturnsTheRequestedSides(
        string message,
        int foodCount,
        int beerCount)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.True(result.Matches.Count == foodCount + beerCount, result.Reply);
        Assert.Equal(foodCount, result.Matches.Count(match =>
            match.Beer.Category!.Type == CategoryType.Food));
        Assert.Equal(beerCount, result.Matches.Count(match =>
            match.Beer.Category!.Type == CategoryType.Beer));
    }

    [Theory]
    [InlineData("beer under 350 MKD", CategoryType.Beer, 350)]
    [InlineData("food under 350 MKD", CategoryType.Food, 350)]
    [InlineData("beer around 300 MKD", CategoryType.Beer, 330)]
    [InlineData("food around 300 MKD", CategoryType.Food, 310)]
    [InlineData("cheapest beer", CategoryType.Beer, 280)]
    [InlineData("cheapest food", CategoryType.Food, 220)]
    public async Task PriceLanguage_RespectsTheRequestedMenuAndBound(
        string message,
        CategoryType type,
        int maximumPrice)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
        {
            Assert.Equal(type, match.Beer.Category!.Type);
            Assert.True(match.Beer.Price <= maximumPrice);
        });
    }

    [Theory]
    [InlineData("do you have sushi?")]
    [InlineData("do you have wine?")]
    [InlineData("do you have cider?")]
    [InlineData("do you have cocktails?")]
    [InlineData("do you have Guinness?")]
    public async Task MissingCatalogueItems_AreNeverReplacedWithRandomProducts(string message)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.Empty(result.Matches);
        Assert.Contains("don't have", result.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("show me six beers under 100 MKD")]
    [InlineData("one food and two beers for 500 MKD")]
    [InlineData("a Macedonian sour under 3% ABV")]
    [InlineData("a vegan burger")]
    public async Task ImpossibleRequests_DoNotReturnDishonestMatches(string message)
    {
        var result = await RebelAiConversationTests.Conversation().Turn(message);

        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task TopicSwitch_BeerToFoodClearsBeerConstraints()
    {
        var chat = RebelAiConversationTests.Conversation();
        await chat.Turn("show me a dark stout");
        var result = await chat.Turn("actually, food instead");

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Food, match.Beer.Category!.Type));
    }

    [Fact]
    public async Task TopicSwitch_FoodToBeerClearsFoodConstraints()
    {
        var chat = RebelAiConversationTests.Conversation();
        await chat.Turn("show me spicy food");
        var result = await chat.Turn("no, a refreshing beer instead");

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
    }

    [Fact]
    public async Task Recovery_MissingItemDoesNotPoisonNextRequest()
    {
        var chat = RebelAiConversationTests.Conversation();
        await chat.Turn("do you have sushi?");
        var result = await chat.Turn("okay, give me a yuzu beer");

        Assert.Equal("Tokyo Lemonade", Assert.Single(result.Matches).Beer.Name);
    }

    [Fact]
    public async Task Recovery_NewObjectiveClearsPreviousStyle()
    {
        var chat = RebelAiConversationTests.Conversation();
        await chat.Turn("show me sour beers");
        var result = await chat.Turn("now give me the highest alcohol beer");

        Assert.Equal("Dark Matter", Assert.Single(result.Matches).Beer.Name);
    }

    [Fact]
    public async Task Reference_CheapestMeansCheapestOfDisplayedProducts()
    {
        var chat = RebelAiConversationTests.Conversation();
        var displayed = await chat.Turn("show me three beers around 350 MKD");
        var result = await chat.Turn("which one is cheapest?");

        var selected = Assert.Single(result.Matches).Beer;
        Assert.Contains(selected.Id, displayed.Matches.Select(match => match.Beer.Id));
        Assert.Equal(displayed.Matches.Min(match => match.Beer.Price), selected.Price);
    }

    [Fact]
    public async Task AlternativeMixedRound_DoesNotRepeatThePreviousRound()
    {
        var chat = RebelAiConversationTests.Conversation();
        var first = await chat.Turn("one food and one beer for 800 MKD");
        var second = await chat.Turn("show me another round");

        Assert.True(second.Matches.Count == 2, second.Reply);
        Assert.Empty(first.Matches.Select(match => match.Beer.Id)
            .Intersect(second.Matches.Select(match => match.Beer.Id)));
    }

    [Theory]
    [InlineData("awesome, thanks")]
    [InlineData("great, thank you")]
    [InlineData("perfect thanks")]
    [InlineData("thanks boss")]
    [InlineData("cheers")]
    [InlineData("nice, thanks broski")]
    [InlineData("thank you, that's perfect")]
    [InlineData("awesome, cheers")]
    public async Task GratitudeAfterRecommendation_ClosesWithoutNewCards(
        string message)
    {
        var chat = RebelAiConversationTests.Conversation();
        await chat.Turn("one food and one beer for 800 MKD");
        var result = await chat.Turn(message);

        Assert.Empty(result.Matches);
        Assert.Contains("Anytime", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("round", result.Reply, StringComparison.OrdinalIgnoreCase);
    }
}
