using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerGuideTestLabService
{
    IReadOnlyList<BeerGuideLabScenarioDefinition> Scenarios { get; }

    IReadOnlyList<AdminBeerGuideLabScenarioViewModel> RunDeterministic(
        IReadOnlyCollection<Product> beers);

    AdminBeerGuideLabScenarioViewModel Evaluate(
        BeerGuideLabScenarioDefinition scenario,
        IReadOnlyList<Product> matches,
        bool isAiTest = false,
        bool usedAi = false,
        string? reply = null);
}

public sealed class BeerGuideLabScenarioDefinition
{
    public required string Key { get; init; }

    public required string Prompt { get; init; }

    public required string Checks { get; init; }

    public int RequestedCount { get; init; } = 3;

    public IReadOnlyList<string> Styles { get; init; } = [];

    public IReadOnlyList<string> Flavours { get; init; } = [];

    public IReadOnlyList<string> Origins { get; init; } = [];

    public IReadOnlyList<string> Pairings { get; init; } = [];

    public decimal? MinimumAbvExclusive { get; init; }

    public decimal? MaximumAbvExclusive { get; init; }

    public int? MaximumBitterness { get; init; }

    public int? MaximumSweetness { get; init; }

    public bool AllowNoMatches { get; init; }
}
