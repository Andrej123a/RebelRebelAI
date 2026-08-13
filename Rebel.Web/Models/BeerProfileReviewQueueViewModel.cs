using Rebel.Domain.Entities;

namespace Rebel.Web.Models;

public sealed class BeerProfileReviewQueueViewModel
{
    public int RemainingCount { get; init; }

    public int? CurrentPosition { get; init; }

    public Product? NextBeer { get; init; }
}
