using Rebel.Domain.Entities;

namespace Rebel.Web.Models;

public sealed class AdminBeerGuideLabViewModel
{
    public IReadOnlyList<AdminBeerGuideLabScenarioViewModel> Scenarios { get; init; } = [];

    public AdminBeerGuideLabScenarioViewModel? AiResult { get; init; }

    public int Passed => Scenarios.Count(scenario => scenario.Status == "pass");

    public int Warnings => Scenarios.Count(scenario => scenario.Status == "warning");

    public int Failed => Scenarios.Count(scenario => scenario.Status == "fail");
}

public sealed class AdminBeerGuideLabScenarioViewModel
{
    public required string Key { get; init; }

    public required string Prompt { get; init; }

    public required string Checks { get; init; }

    public required string Status { get; init; }

    public int RequestedCount { get; init; }

    public bool IsAiTest { get; init; }

    public bool UsedAi { get; init; }

    public string? Reply { get; init; }

    public IReadOnlyList<Product> Beers { get; init; } = [];

    public IReadOnlyList<AdminBeerGuideLabIssueViewModel> Issues { get; init; } = [];
}

public sealed class AdminBeerGuideLabIssueViewModel
{
    public required string Severity { get; init; }

    public required string Message { get; init; }

    public Guid? ProductId { get; init; }

    public string? ProductName { get; init; }
}
