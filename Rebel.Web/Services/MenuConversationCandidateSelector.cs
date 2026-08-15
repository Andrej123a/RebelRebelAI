using Rebel.Domain.Entities;

namespace Rebel.Web.Services;

public static class MenuConversationCandidateSelector
{
    public static IReadOnlyList<Product> Select(
        IReadOnlyCollection<Product> products,
        IReadOnlyCollection<Guid> excludedProductIds,
        IReadOnlyList<Guid> previousProductIds,
        string message)
    {
        var similarity = previousProductIds.Count > 0 &&
            BeerChatContextPolicy.RequestsSimilarityToPrevious(message);
        var alternative = !similarity && previousProductIds.Count > 0 &&
            BeerChatContextPolicy.RequestsAlternatives(message);
        var comparison = !alternative && previousProductIds.Count > 0 &&
            BeerChatContextPolicy.RefersToPreviousResults(message);
        var selected = products
            .Where(product =>
                !excludedProductIds.Contains(product.Id) &&
                (!comparison || previousProductIds.Contains(product.Id)) &&
                (!alternative || !previousProductIds.Contains(product.Id)))
            .ToList();

        if (!comparison && !similarity)
        {
            return selected;
        }

        var previousOrder = previousProductIds
            .Select((id, index) => new { id, index })
            .ToDictionary(item => item.id, item => item.index);
        return selected
            .OrderBy(product => previousOrder.GetValueOrDefault(product.Id, int.MaxValue))
            .ToList();
    }
}
