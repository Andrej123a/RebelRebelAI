using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerProfileReviewQueueService
{
    BeerProfileReviewQueueViewModel Build(
        IReadOnlyCollection<Product> beers,
        Guid currentBeerId);
}
