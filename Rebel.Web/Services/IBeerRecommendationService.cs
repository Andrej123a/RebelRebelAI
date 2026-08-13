using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerRecommendationService
{
    IReadOnlyList<BeerGuideRecommendation> Recommend(
        IReadOnlyCollection<Product> beers,
        Product? food,
        BeerGuideRequest request);
}
