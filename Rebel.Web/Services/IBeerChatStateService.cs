using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerChatStateService
{
    BeerChatStateUpdate Update(string message, BeerChatPreferenceState? previous);
}
