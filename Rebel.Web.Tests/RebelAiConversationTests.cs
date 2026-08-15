using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Models;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class RebelAiConversationTests
{
    [Fact]
    public async Task FoodConversation_CanAskForDifferentChoicesWithoutRepeats()
    {
        var chat = Conversation();
        var first = await chat.Turn("show me three spicy foods");
        var second = await chat.Turn("what else do you have?");

        Assert.Equal(3, first.Matches.Count);
        Assert.NotEmpty(second.Matches);
        Assert.All(second.Matches, match =>
            Assert.Equal(CategoryType.Food, match.Beer.Category!.Type));
        Assert.Empty(first.Matches.Select(match => match.Beer.Id)
            .Intersect(second.Matches.Select(match => match.Beer.Id)));
        Assert.Contains("different plates", second.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Conversation_CanSwitchFromBeerToFoodNaturally()
    {
        var chat = Conversation();
        var beer = await chat.Turn("show me refreshing beers");
        var switchPrompt = await chat.Turn("and food?");
        var food = await chat.Turn("something spicy");

        Assert.All(beer.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
        Assert.Empty(switchPrompt.Matches);
        Assert.Contains("Food it is", switchPrompt.Reply);
        Assert.All(food.Matches, match =>
            Assert.Equal(CategoryType.Food, match.Beer.Category!.Type));
        Assert.Equal("food", chat.Preferences?.ItemKind);
    }

    [Fact]
    public async Task AmbiguousBudget_RemembersPriceAfterFoodChoice()
    {
        var chat = Conversation();
        var question = await chat.Turn("something around 300 MKD");
        var answer = await chat.Turn("Food");

        Assert.Empty(question.Matches);
        Assert.Contains("beer or food", question.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(answer.Matches);
        Assert.All(answer.Matches, match =>
            Assert.Equal(CategoryType.Food, match.Beer.Category!.Type));
        Assert.Equal(290m, answer.Matches[0].Beer.Price);
    }

    [Fact]
    public async Task ComparisonFollowUp_ReturnsOneCheapestPreviousBeer()
    {
        var chat = Conversation();
        var first = await chat.Turn("show me three beers around 350 MKD");
        var compared = await chat.Turn("which one is cheaper?");

        var match = Assert.Single(compared.Matches);
        Assert.Contains(match.Beer.Id, first.Matches.Select(item => item.Beer.Id));
        Assert.Equal(first.Matches.Min(item => item.Beer.Price), match.Beer.Price);
    }

    [Fact]
    public async Task OrdinalFollowUp_ExplainsTheDisplayedSecondProduct()
    {
        var chat = Conversation();
        var first = await chat.Turn("show me three beers around 350 MKD");
        var profile = await chat.Turn("tell me more about the second one");

        var match = Assert.Single(profile.Matches);
        Assert.Equal(first.Matches[1].Beer.Id, match.Beer.Id);
        Assert.Contains(first.Matches[1].Beer.Name, profile.Reply);
    }

    [Fact]
    public async Task MissingMenuItem_DoesNotPoisonTheNextDirection()
    {
        var chat = Conversation();
        var missing = await chat.Turn("do you have sushi?");
        var next = await chat.Turn("okay, show me a yuzu beer");

        Assert.Empty(missing.Matches);
        Assert.Contains("don't have sushi", missing.Reply, StringComparison.OrdinalIgnoreCase);
        var match = Assert.Single(next.Matches);
        Assert.Equal("Tokyo Lemonade", match.Beer.Name);
    }

    [Fact]
    public async Task FoodConversation_UnderstandsLessSpicyAsARefinement()
    {
        var chat = Conversation();
        var spicy = await chat.Turn("show me spicy food");
        var milder = await chat.Turn("less spicy please");

        Assert.True(spicy.Matches[0].Beer.HeatLevel >= 4);
        Assert.NotEmpty(milder.Matches);
        Assert.All(milder.Matches, match => Assert.True(match.Beer.HeatLevel <= 2));
        Assert.Equal("low", chat.Preferences?.Heat);
    }

    [Fact]
    public async Task FoodConversation_UnderstandsLessSaltyAsARefinement()
    {
        var chat = Conversation();
        var salty = await chat.Turn("show me salty food");
        var lighter = await chat.Turn("less salty please");

        Assert.True(salty.Matches[0].Beer.SaltinessLevel >= 4);
        Assert.Equal(1, lighter.Matches[0].Beer.SaltinessLevel);
        Assert.Equal("low", chat.Preferences?.Saltiness);
    }

    [Fact]
    public async Task NamedFoodQuestion_ReturnsItsProfileInsteadOfRandomFood()
    {
        var chat = Conversation();
        var result = await chat.Turn("tell me about Katsu Burger");

        var match = Assert.Single(result.Matches);
        Assert.Equal("Katsu Burger", match.Beer.Name);
        Assert.Contains("crispy chicken", result.Reply);
        Assert.Contains("410 MKD", result.Reply);
    }

    [Fact]
    public async Task NaturalDrinkPairingQuestion_ReturnsBeerForTheFood()
    {
        var chat = Conversation();
        var result = await chat.Turn("what should I drink with Buffalo Wings?");

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
        Assert.Contains(result.Matches, match => match.Beer.Name == "Citrus Riot");
    }

    [Fact]
    public async Task BeerPairing_CanBeRefinedWithoutFallingBackToFood()
    {
        var chat = Conversation();
        await chat.Turn("what beer should I drink with Buffalo Wings?");
        var refined = await chat.Turn("something less bitter");

        Assert.True(refined.Matches.Count > 0, refined.Reply);
        Assert.All(refined.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
        Assert.Equal("beer", chat.Preferences?.ItemKind);
    }

    [Fact]
    public async Task NamedBeer_CanLeadIntoGenericSimilarBeerRequest()
    {
        var chat = Conversation();
        var profile = await chat.Turn("tell me about Tokyo Lemonade");
        var similar = await chat.Turn("yes, show me similar beers");

        Assert.Single(profile.Matches);
        Assert.True(similar.Matches.Count > 0, similar.Reply);
        Assert.All(similar.Matches, match =>
            Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type));
        Assert.DoesNotContain(similar.Matches, match =>
            match.Beer.Name == "Tokyo Lemonade");
        Assert.Contains("similar profile", similar.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MixedBudgetOrder_ReturnsOneFoodAndTwoBeersWithinTheTotal()
    {
        var chat = Conversation();
        var result = await chat.Turn(
            "hello broski, got 1000 to spend, give me a food and 2 beers. what do you recommend?");

        Assert.True(result.Matches.Count == 3, result.Reply);
        Assert.Single(result.Matches, match =>
            match.Beer.Category!.Type == CategoryType.Food);
        Assert.Equal(2, result.Matches.Count(match =>
            match.Beer.Category!.Type == CategoryType.Beer));
        Assert.True(result.Matches.Sum(match => match.Beer.Price) <= 1000m);
        Assert.Contains("round", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MKD", result.Reply);
    }

    [Fact]
    public async Task DelegatedChoice_UsesTheRememberedMixedOrderBrief()
    {
        var chat = Conversation();
        var first = await chat.Turn("one food and two beers for 1000 MKD");
        var result = await chat.Turn("it's on you");

        Assert.Equal(3, result.Matches.Count);
        Assert.Contains("my call", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Matches.Sum(match => match.Beer.Price) <= 1000m);
        Assert.Empty(first.Matches.Select(match => match.Beer.Id)
            .Intersect(result.Matches.Select(match => match.Beer.Id)));
    }

    [Fact]
    public async Task VagueRecommendation_AsksWhichSideOfTheMenu()
    {
        var chat = Conversation();
        var result = await chat.Turn("what do you recommend?");

        Assert.Empty(result.Matches);
        Assert.Contains("beer", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("eat", result.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, result.FollowUps?.Count);
        Assert.True(result.Reply.Length < 120);
    }

    [Fact]
    public async Task SuggestionClarification_UnderstandsBothAsOneOfEach()
    {
        var chat = Conversation();
        var question = await chat.Turn("what would you suggest me?");
        var answer = await chat.Turn("both");

        Assert.Empty(question.Matches);
        Assert.Equal(3, question.FollowUps?.Count);
        Assert.Equal(2, answer.Matches.Count);
        Assert.Single(answer.Matches, match =>
            match.Beer.Category!.Type == CategoryType.Food);
        Assert.Single(answer.Matches, match =>
            match.Beer.Category!.Type == CategoryType.Beer);
        Assert.Contains("round", answer.Reply, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenericBeerRecommendation_GivesACompactHousePick()
    {
        var chat = Conversation();
        var result = await chat.Turn("recommend me a beer");

        var match = Assert.Single(result.Matches);
        Assert.Equal(CategoryType.Beer, match.Beer.Category!.Type);
        Assert.True(match.Beer.Price <= 500m);
        Assert.Contains("Leaving it to me", result.Reply);
        Assert.True(result.Reply.Length < 220);
    }

    [Fact]
    public async Task MixedOrder_WithDescriptiveQuantitiesReturnsTheCompleteRound()
    {
        var chat = Conversation();
        var result = await chat.Turn(
            "one spicy food and two light beers for 1000 MKD");

        Assert.True(result.Matches.Count == 3, result.Reply);
        Assert.Single(result.Matches, match =>
            match.Beer.Category!.Type == CategoryType.Food);
        var beers = result.Matches
            .Where(match => match.Beer.Category!.Type == CategoryType.Beer)
            .Select(match => match.Beer)
            .ToList();
        Assert.Equal(2, beers.Count);
        Assert.All(beers, beer => Assert.True(beer.AlcoholByVolume <= 5m));
        Assert.True(result.Matches.Sum(match => match.Beer.Price) <= 1000m);
    }

    [Theory]
    [InlineData("no food, just recommend me a beer", CategoryType.Beer)]
    [InlineData("no beer, just recommend me food", CategoryType.Food)]
    public async Task NegatedMenuSide_NeverReturnsTheRejectedKind(
        string message,
        CategoryType expectedType)
    {
        var chat = Conversation();
        var result = await chat.Turn(message);

        Assert.NotEmpty(result.Matches);
        Assert.All(result.Matches, match =>
            Assert.Equal(expectedType, match.Beer.Category!.Type));
    }

    [Fact]
    public async Task MixedOrder_RespectsRequestedFoodCategoryAndBeerStyle()
    {
        var chat = Conversation();
        var result = await chat.Turn(
            "pick one burger and two IPAs for no more than 1200 MKD");

        Assert.True(result.Matches.Count == 3, result.Reply);
        var food = Assert.Single(result.Matches, match =>
            match.Beer.Category!.Type == CategoryType.Food).Beer;
        Assert.Contains("burger", food.Name, StringComparison.OrdinalIgnoreCase);
        Assert.All(result.Matches.Where(match =>
            match.Beer.Category!.Type == CategoryType.Beer), match =>
                Assert.Contains("IPA", match.Beer.BeerStyle, StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Matches.Sum(match => match.Beer.Price) <= 1200m);
    }

    internal static ConversationHarness Conversation() =>
        new(BuildMenu());

    private static IReadOnlyList<Product> BuildMenu() =>
    [
        Beer("Tokyo Lemonade", "Yuzu witbier", "yuzu, orange, wheat, bright citrus", 390m, 4.2m, 2),
        Beer("Red Pill", "Fruit sour", "strawberry, lime, refreshing tartness", 380m, 3.5m, 1, "buffalo wings", 1),
        Beer("Easy Lager", "Lager", "clean, crisp, light citrus", 280m, 4.5m, 2),
        Beer("Citrus Riot", "IPA", "grapefruit, citrus, pine", 350m, 6.2m, 3, "buffalo wings, burger"),
        Beer("Blue Pill", "West Coast IPA", "citrus, pine, black tea", 330m, 6m, 3),
        Beer("Dark Matter", "Stout", "coffee, chocolate, roasted malt", 420m, 7.5m, 5),
        Food("Buffalo Wings", "Wings", "buffalo sauce, ranch", 290m, 5, 4, 4),
        Food("Hot Honey Wings", "Wings", "hot honey, ranch", 290m, 4, 4, 3),
        Food("Dirty Fries", "Fries", "cheese, sauce, crispy fries", 310m, 2, 4, 5),
        Food("Pepperoni Pizza", "Pizza", "pepperoni, cheese, tomato", 390m, 2, 5, 5),
        Food("Katsu Burger", "Burgers", "crispy chicken, slaw, sauce", 410m, 1, 5, 3),
        Food("Fresh Salad", "Salads", "fresh greens, tomato, light dressing", 220m, 1, 1, 1)
    ];

    private static Product Beer(
        string name,
        string style,
        string notes,
        decimal price,
        decimal abv,
        int body,
        string pairings = "",
        int bitterness = 2) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        BeerStyle = style,
        FlavorNotes = notes,
        Price = price,
        AlcoholByVolume = abv,
        BodyLevel = body,
        BitternessLevel = bitterness,
        SweetnessLevel = 2,
        AcidityLevel = 2,
        PairingTags = pairings,
        IsAvailable = true,
        Category = new Category { Name = "Beer", Type = CategoryType.Beer }
    };

    private static Product Food(
        string name,
        string category,
        string notes,
        decimal price,
        int heat,
        int richness,
        int saltiness) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        FlavorNotes = notes,
        Price = price,
        HeatLevel = heat,
        SaltinessLevel = saltiness,
        RichnessLevel = richness,
        SweetnessLevel = 2,
        AcidityLevel = 2,
        IsAvailable = true,
        Category = new Category { Name = category, Type = CategoryType.Food }
    };

    internal sealed class ConversationHarness
    {
        private readonly IReadOnlyList<Product> _menu;
        private readonly BeerChatStateService _state = new(new BeerPreferenceParser());
        private readonly OpenAiBeerGuideChatService _chat;
        private IReadOnlyList<Guid> _previousIds = [];

        public ConversationHarness(IReadOnlyList<Product> menu)
        {
            _menu = menu;
            _chat = new OpenAiBeerGuideChatService(
                new ConfigurationBuilder().Build(),
                new BeerCatalogMatcher(),
                NullLogger<OpenAiBeerGuideChatService>.Instance,
                foodMatcher: new FoodCatalogMatcher());
        }

        public BeerChatPreferenceState? Preferences { get; private set; }

        public async Task<BeerChatResult> Turn(string message)
        {
            var update = _state.Update(message, Preferences);
            var candidates = MenuConversationCandidateSelector.Select(
                _menu,
                [],
                _previousIds,
                message);
            var beers = candidates
                .Where(product => product.Category?.Type == CategoryType.Beer)
                .ToList();
            var result = await _chat.ReplyStructuredAsync(
                message,
                update.EffectiveQuery,
                beers,
                new Dictionary<Guid, double>(),
                CancellationToken.None,
                candidates);

            Preferences = update.Preferences;
            if (result.Matches.Count > 0)
            {
                _previousIds = result.Matches.Select(match => match.Beer.Id).ToList();
            }

            return result;
        }
    }
}
