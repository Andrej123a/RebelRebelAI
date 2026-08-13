using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public sealed class BeerProfileReviewQueueService : IBeerProfileReviewQueueService
{
    public BeerProfileReviewQueueViewModel Build(
        IReadOnlyCollection<Product> beers,
        Guid currentBeerId)
    {
        var queue = beers
            .Where(beer => beer.IsAvailable)
            .Where(beer =>
                BeerProfileQuality.MissingFields(beer).Count > 0 ||
                BeerProfileQuality.ReviewNotes(beer).Count > 0)
            .OrderBy(beer => beer.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(beer => beer.Id)
            .ToList();

        var currentIndex = queue.FindIndex(beer => beer.Id == currentBeerId);
        var nextBeer = queue
            .Skip(currentIndex >= 0 ? currentIndex + 1 : 0)
            .FirstOrDefault(beer => beer.Id != currentBeerId)
            ?? queue.FirstOrDefault(beer => beer.Id != currentBeerId);

        return new BeerProfileReviewQueueViewModel
        {
            RemainingCount = queue.Count,
            CurrentPosition = currentIndex >= 0 ? currentIndex + 1 : null,
            NextBeer = nextBeer
        };
    }
}
