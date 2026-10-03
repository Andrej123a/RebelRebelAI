using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerMenuPresenterTests
{
    [Theory]
    [InlineData("West Coast IPA", "hoppy")]
    [InlineData("American Pale Ale", "hoppy")]
    [InlineData("Red IPA", "hoppy")]
    [InlineData("Imperial Stout", "dark")]
    [InlineData("Black IPA", "dark")]
    [InlineData("Fruited IPA", "sour")]
    [InlineData("Gose", "sour")]
    [InlineData("Kölsch", "crisp")]
    [InlineData("Czech Pils", "crisp")]
    [InlineData("Amber Lager", "amber")]
    [InlineData("Belgian Tripel", "belgian")]
    [InlineData("Hefeweizen", "belgian")]
    [InlineData("Experimental", "wildcard")]
    [InlineData(null, "wildcard")]
    public void ClassifyFamily_MapsStylesToColourFamilies(string? style, string expected)
    {
        Assert.Equal(expected, BeerMenuPresenter.ClassifyFamily(style).Key);
    }

    [Fact]
    public void ClassifyFamily_FallsBackToCategoryName()
    {
        Assert.Equal("dark", BeerMenuPresenter.ClassifyFamily(null, "Stouts & Porters").Key);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 50)]
    [InlineData(5, 100)]
    [InlineData(9, 100)]
    public void LevelPercent_SpreadsOneToFiveAcrossTheBar(int level, int expected)
    {
        Assert.Equal(expected, BeerMenuPresenter.LevelPercent(level));
    }

    [Theory]
    [InlineData(2.5, 0)]
    [InlineData(3.0, 0)]
    [InlineData(6.5, 50)]
    [InlineData(10.0, 100)]
    [InlineData(12.0, 100)]
    public void StrengthPercent_RunsFromSessionToStrong(double abv, int expected)
    {
        Assert.Equal(expected, BeerMenuPresenter.StrengthPercent((decimal)abv));
    }

    [Fact]
    public void CreateAnchor_SlugifiesAndKeepsAnchorsUnique()
    {
        var used = new HashSet<string>();

        Assert.Equal("beer-big-joe", BeerMenuPresenter.CreateAnchor("Big Joe!", 1, used));
        Assert.Equal("beer-big-joe-2", BeerMenuPresenter.CreateAnchor("big joe", 2, used));
        Assert.Equal("beer-kolsch", BeerMenuPresenter.CreateAnchor("Kölsch", 3, used));
        Assert.Equal("beer-04", BeerMenuPresenter.CreateAnchor("Пиво", 4, used));
    }

    [Fact]
    public void Build_OrdersSidesByCategoryAndPutsAvailableBeersFirst()
    {
        var bottles = Category("Bottles");
        var draft = Category("Draft");

        var menu = BeerMenuPresenter.Build(
        [
            Beer("Zulu", draft, "Pils"),
            Beer("Alpha", draft, "Stout", available: false),
            Beer("Mojo", draft, "IPA", popular: true),
            Beer("Bravo", bottles, "Gose")
        ]);

        Assert.Equal(["Bottles", "Draft"], menu.Sides.Select(side => side.CategoryName));
        Assert.Equal(["Side A", "Side B"], menu.Sides.Select(side => side.Label));
        Assert.Equal(["Bravo", "Mojo", "Zulu", "Alpha"], menu.Tracks.Select(track => track.Name));
        Assert.Equal([1, 2, 3, 4], menu.Tracks.Select(track => track.Number));
        Assert.Equal(4, menu.TotalCount);
        Assert.Equal(3, menu.AvailableCount);
    }

    [Fact]
    public void Build_SkipsFoodDeletedBeersAndDeletedCategories()
    {
        var beer = Category("Draft");
        var food = Category("Burgers", CategoryType.Food);
        var closed = Category("Old taps");
        closed.IsDeleted = true;

        var deletedBeer = Beer("Gone", beer, "IPA");
        deletedBeer.IsDeleted = true;

        var menu = BeerMenuPresenter.Build(
        [
            Beer("Kept", beer, "IPA"),
            deletedBeer,
            Beer("Smash", food, null),
            Beer("Retired", closed, "Lager")
        ]);

        Assert.Equal(["Kept"], menu.Tracks.Select(track => track.Name));
    }

    [Fact]
    public void Build_InvertsEverySecondNeighbourFromTheSameFamily()
    {
        var draft = Category("Draft");

        var menu = BeerMenuPresenter.Build(
        [
            Beer("A", draft, "IPA"),
            Beer("B", draft, "NEIPA"),
            Beer("C", draft, "DIPA"),
            Beer("D", draft, "Stout")
        ]);

        var tracks = menu.Tracks.ToList();

        Assert.Equal([false, true, false, false], tracks.Select(track => track.IsInverted));
        Assert.Equal(BeerMenuPresenter.Hoppy.Background, tracks[0].StageBackground);
        Assert.Equal("#0E0807", tracks[1].StageBackground);
        Assert.Equal(BeerMenuPresenter.Hoppy.Glow, tracks[1].Accent);
        Assert.Equal(BeerMenuPresenter.Dark.Background, tracks[3].StageBackground);
    }

    [Fact]
    public void Build_SoldOutBeersPlayOnAMutedStage()
    {
        var draft = Category("Draft");

        var menu = BeerMenuPresenter.Build([Beer("Gone", draft, "Sour", available: false)]);
        var track = Assert.Single(menu.Tracks);

        Assert.False(track.IsInverted);
        Assert.Equal("#2A1310", track.StageBackground);
        Assert.Equal(BeerMenuPresenter.Sour.Glow, track.Accent);
    }

    [Fact]
    public void Build_OnlyShowsTasteScalesThatHaveData()
    {
        var draft = Category("Draft");
        var beer = Beer("Mojo", draft, "APA");
        beer.BodyLevel = 4;
        beer.BitternessLevel = null;
        beer.AlcoholByVolume = 4.7m;

        var track = Assert.Single(BeerMenuPresenter.Build([beer]).Tracks);

        Assert.Equal(["body", "strength"], track.Taste.Select(scale => scale.Key));
        Assert.Equal(75, track.Taste[0].Percent);
        Assert.Equal("Strength: 4.7% ABV", track.Taste[1].Reading);
    }

    [Fact]
    public void Build_SplitsNotesAndPairingsAndListsOnlyFamiliesInUse()
    {
        var draft = Category("Draft");
        var beer = Beer("Mojo", draft, "APA");
        beer.FlavorNotes = "citrus, pine,  Citrus ,grapefruit";
        beer.PairingTags = "burger; pizza";

        var menu = BeerMenuPresenter.Build([beer]);
        var track = Assert.Single(menu.Tracks);

        Assert.Equal(["citrus", "pine", "grapefruit"], track.FlavorNotes);
        Assert.Equal(["burger", "pizza"], track.Pairings);
        Assert.Equal(["hoppy"], menu.Families.Select(family => family.Key));
        Assert.Contains("grapefruit", track.SearchText);
        Assert.Contains("hoppy & hazy", track.SearchText);
    }

    [Theory]
    [InlineData("Mojo", "")]
    [InlineData("Hefeweizen", "has-long-name")]
    [InlineData("Weihenstephaner", "has-xl-name")]
    public void NameScale_ShrinksLongNamesSoTheyFit(string name, string expected)
    {
        var track = Assert.Single(BeerMenuPresenter.Build([Beer(name, Category("Draft"), "Lager")]).Tracks);

        Assert.Equal(expected, track.NameScale);
    }

    private static Category Category(string name, CategoryType type = CategoryType.Beer) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type
        };

    private static Product Beer(
        string name,
        Category category,
        string? style,
        bool available = true,
        bool popular = false) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            BeerStyle = style,
            Price = 300m,
            IsAvailable = available,
            IsPopular = popular,
            Category = category,
            CategoryId = category.Id
        };
}
