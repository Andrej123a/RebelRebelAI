using Rebel.Domain.Entities;
using Rebel.Web.Services;
using Xunit;

namespace Rebel.Web.Tests;

public sealed class BeerGuideTestLabServiceTests
{
    private readonly BeerGuideTestLabService _lab = new(new BeerCatalogMatcher());

    [Fact]
    public void Scenarios_ContainsPermanentCoreChecks()
    {
        Assert.Equal(15, _lab.Scenarios.Count);
        Assert.Contains(_lab.Scenarios, scenario => scenario.Key == "grapefruit-ipa");
        Assert.Contains(_lab.Scenarios, scenario => scenario.Key == "burger-pairing");
        Assert.Contains(_lab.Scenarios, scenario => scenario.Key == "under-five");
    }

    [Fact]
    public void Evaluate_PassesWhenReturnedBeerMeetsRule()
    {
        var scenario = new BeerGuideLabScenarioDefinition
        {
            Key = "under-five",
            Prompt = "Beer under 5%",
            Checks = "ABV below 5%",
            RequestedCount = 1,
            MaximumAbvExclusive = 5m
        };

        var result = _lab.Evaluate(scenario, [Beer("Easy Lager", abv: 4.6m)]);

        Assert.Equal("pass", result.Status);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Evaluate_FailsWhenUnavailableBeerIsReturned()
    {
        var beer = Beer("Sold Out IPA", style: "IPA", flavours: "grapefruit");
        beer.IsAvailable = false;
        var scenario = StyleScenario();

        var result = _lab.Evaluate(scenario, [beer]);

        Assert.Equal("fail", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Message.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_FailsWhenStyleDoesNotMatch()
    {
        var result = _lab.Evaluate(
            StyleScenario(),
            [Beer("Dark Matter", style: "Stout", flavours: "coffee")]);

        Assert.Equal("fail", result.Status);
        Assert.Contains(result.Issues, issue => issue.Message.Contains("style"));
    }

    [Fact]
    public void Evaluate_WarnsWhenRequiredProfileDataIsMissing()
    {
        var result = _lab.Evaluate(
            StyleScenario(),
            [Beer("Mystery Beer", style: null, flavours: "grapefruit")]);

        Assert.Equal("warning", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Message.Contains("profile is empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Evaluate_WarnsForAnExpectedCatalogueGap()
    {
        var scenario = new BeerGuideLabScenarioDefinition
        {
            Key = "missing-origin",
            Prompt = "A Czech stout",
            Checks = "Honest no-match",
            RequestedCount = 1,
            AllowNoMatches = true
        };

        var result = _lab.Evaluate(scenario, []);

        Assert.Equal("warning", result.Status);
        Assert.Contains(result.Issues, issue =>
            issue.Message.Contains("No exact catalogue match", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeterministicRun_NeverReturnsUnavailableBeer()
    {
        var available = Beer("Citrus Riot", style: "IPA", flavours: "citrus grapefruit");
        var unavailable = Beer("Better Grapefruit", style: "IPA", flavours: "grapefruit");
        unavailable.IsAvailable = false;

        var results = _lab.RunDeterministic([available, unavailable]);

        Assert.DoesNotContain(
            results.SelectMany(result => result.Beers),
            beer => beer.Id == unavailable.Id);
    }

    private static BeerGuideLabScenarioDefinition StyleScenario() => new()
    {
        Key = "grapefruit-ipa",
        Prompt = "A grapefruit IPA",
        Checks = "IPA and grapefruit",
        RequestedCount = 1,
        Styles = ["ipa"],
        Flavours = ["grapefruit"]
    };

    private static Product Beer(
        string name,
        string? style = "Lager",
        string? flavours = "clean",
        decimal? abv = 5m) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsAvailable = true,
        BeerStyle = style,
        FlavorNotes = flavours,
        AlcoholByVolume = abv
    };
}
