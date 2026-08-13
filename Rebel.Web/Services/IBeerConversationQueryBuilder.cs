using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerConversationQueryBuilder
{
    string Build(
        string message,
        IReadOnlyList<BeerChatTurn> history);
}
