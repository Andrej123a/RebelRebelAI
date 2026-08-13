using Rebel.Domain.Entities;

namespace Rebel.Web.Models;

public sealed class AdminBeerCatalogViewModel
{
    public IReadOnlyList<AdminBeerCatalogItemViewModel> Ready { get; init; } = [];

    public IReadOnlyList<AdminBeerCatalogItemViewModel> MissingInformation { get; init; } = [];

    public IReadOnlyList<AdminBeerCatalogItemViewModel> NeedsReview { get; init; } = [];

    public int Total => Ready.Count + MissingInformation.Count + NeedsReview.Count;
}

public sealed class AdminBeerCatalogItemViewModel
{
    public required Product Beer { get; init; }

    public int CompletionPercent { get; init; }

    public IReadOnlyList<string> MissingFields { get; init; } = [];

    public IReadOnlyList<string> ReviewNotes { get; init; } = [];

    public IReadOnlyList<AdminBeerProfileTestViewModel> Tests { get; init; } = [];

    public IReadOnlyList<BeerProfileSuggestionViewModel> Suggestions { get; init; } = [];
}

public sealed class AdminBeerProfileTestViewModel
{
    public required string Prompt { get; init; }

    public int? Rank { get; init; }
}
