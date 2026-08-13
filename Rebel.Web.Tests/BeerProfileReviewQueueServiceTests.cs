using Rebel.Domain.Entities;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerProfileReviewQueueServiceTests
{
    private readonly BeerProfileReviewQueueService _service = new();

    [Fact]
    public void Build_ContainsOnlyAvailableIncompleteProfiles()
    {
        var incomplete = IncompleteBeer("Alpha");
        var unavailable = IncompleteBeer("Beta");
        unavailable.IsAvailable = false;
        var complete = CompleteBeer("Gamma");

        var result = _service.Build([incomplete, unavailable, complete], incomplete.Id);

        Assert.Equal(1, result.RemainingCount);
        Assert.Equal(1, result.CurrentPosition);
        Assert.Null(result.NextBeer);
    }

    [Fact]
    public void Build_SelectsNextIncompleteBeerAlphabetically()
    {
        var alpha = IncompleteBeer("Alpha");
        var beta = IncompleteBeer("Beta");
        var gamma = IncompleteBeer("Gamma");

        var result = _service.Build([gamma, alpha, beta], alpha.Id);

        Assert.Equal(3, result.RemainingCount);
        Assert.Equal(1, result.CurrentPosition);
        Assert.Equal(beta.Id, result.NextBeer?.Id);
    }

    [Fact]
    public void Build_WrapsToFirstBeerAtEndOfQueue()
    {
        var alpha = IncompleteBeer("Alpha");
        var gamma = IncompleteBeer("Gamma");

        var result = _service.Build([gamma, alpha], gamma.Id);

        Assert.Equal(2, result.CurrentPosition);
        Assert.Equal(alpha.Id, result.NextBeer?.Id);
    }

    [Fact]
    public void Build_AfterCurrentBecomesComplete_SelectsFirstRemainingBeer()
    {
        var current = CompleteBeer("Alpha");
        var next = IncompleteBeer("Beta");

        var result = _service.Build([current, next], current.Id);

        Assert.Equal(1, result.RemainingCount);
        Assert.Null(result.CurrentPosition);
        Assert.Equal(next.Id, result.NextBeer?.Id);
    }

    [Fact]
    public void Build_ReturnsEmptyQueueWhenAllProfilesAreReady()
    {
        var current = CompleteBeer("Alpha");

        var result = _service.Build([current], current.Id);

        Assert.Equal(0, result.RemainingCount);
        Assert.Null(result.CurrentPosition);
        Assert.Null(result.NextBeer);
    }

    private static Product IncompleteBeer(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsAvailable = true
    };

    private static Product CompleteBeer(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsAvailable = true,
        OriginCountry = "Germany",
        BeerStyle = "Lager",
        AlcoholByVolume = 5m,
        BodyLevel = 2,
        BitternessLevel = 2,
        SweetnessLevel = 2,
        FlavorNotes = "clean",
        PairingTags = "pizza"
    };
}
