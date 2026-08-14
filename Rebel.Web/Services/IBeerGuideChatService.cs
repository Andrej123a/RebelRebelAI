using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerGuideChatService
{
    Task<BeerChatResult> ReplyAsync(
        string message,
        IReadOnlyList<BeerChatTurn> history,
        IReadOnlyCollection<Product> beers,
        IReadOnlyDictionary<Guid, double> feedbackScores,
        CancellationToken cancellationToken);

    Task<BeerChatResult> ReplyStructuredAsync(
        string message,
        string effectiveQuery,
        IReadOnlyCollection<Product> beers,
        IReadOnlyDictionary<Guid, double> feedbackScores,
        CancellationToken cancellationToken,
        IReadOnlyCollection<Product>? menuProducts = null);
}
