namespace Rebel.Web.Models;

public sealed class BeerProfileSuggestionViewModel
{
    public required string Field { get; init; }

    public required string Label { get; init; }

    public required string Value { get; init; }

    public required string Evidence { get; init; }
}
