using Rebel.Domain.Entities;

namespace Rebel.Web.Services;

public interface IBeerCatalogMatcher
{
    IReadOnlyList<Product> Shortlist(
        string query,
        IReadOnlyCollection<Product> beers,
        int limit,
        IReadOnlyDictionary<Guid, double>? feedbackScores = null,
        bool includeUnavailable = false);

    string BuildEvidenceReason(Product beer, string query);

    bool HasUsefulPreference(string query);
}
