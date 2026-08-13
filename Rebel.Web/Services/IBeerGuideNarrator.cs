using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerGuideNarrator
{
    Task<BeerGuideNarrationResult> EnrichAsync(
        BeerGuideRequest request,
        Product? food,
        IReadOnlyList<BeerGuideRecommendation> recommendations,
        CancellationToken cancellationToken);
}

public sealed record BeerGuideNarrationResult(
    IReadOnlyList<BeerGuideRecommendation> Recommendations,
    bool UsedAi);
