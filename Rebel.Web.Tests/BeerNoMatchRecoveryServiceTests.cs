using Rebel.Domain.Entities;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerNoMatchRecoveryServiceTests
{
    private readonly BeerNoMatchRecoveryService _service = new();

    [Fact]
    public void Build_BudgetFailureOffersRaisedBudgetAndCheapestOption()
    {
        var recovery = _service.Build(
            "IPA under 200 denars",
            [
                Beer("Value Lager", 220m, 5m, "Lager"),
                Beer("Available IPA", 330m, 6m, "IPA")
            ]);

        Assert.Contains("budget", recovery.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("340 MKD"));
        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("cheapest"));
    }

    [Fact]
    public void Build_AbvFailureOffersRealisticLimitFromAvailableMenu()
    {
        var recovery = _service.Build(
            "lager under 3% ABV",
            [Beer("Easy Lager", 250m, 4.5m)]);

        Assert.Contains("alcohol limit", recovery.Reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("5%"));
    }

    [Fact]
    public void Build_StyleAndFlavourFailureOffersSpecificRelaxations()
    {
        var recovery = _service.Build(
            "grapefruit stout",
            [Beer("Clean Lager", 250m, 5m)]);

        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("different style"));
        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("nearby flavour"));
    }

    [Fact]
    public void Build_LowBitternessFailureOffersMediumBitterness()
    {
        var recovery = _service.Build(
            "IPA that is not bitter",
            [Beer("Firm IPA", 300m, 6m)]);

        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("medium bitterness"));
    }

    [Theory]
    [InlineData("sour?")]
    [InlineData("sour beer")]
    public void Build_MissingSourExplainsAvailabilityAndOffersUsefulDirections(string query)
    {
        var recovery = _service.Build(
            query,
            [Beer("Clean Lager", 250m, 5m, "Lager")]);

        Assert.Contains("do not have a sour beer available", recovery.Reply,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("fruity"));
        Assert.Contains(recovery.FollowUps, item => item.Label.Contains("citrusy"));
        Assert.DoesNotContain(recovery.FollowUps, item => item.Label == "Try a different style");
    }

    private static Product Beer(
        string name,
        decimal price,
        decimal abv,
        string? style = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Price = price,
        AlcoholByVolume = abv,
        BeerStyle = style,
        IsAvailable = true
    };
}
