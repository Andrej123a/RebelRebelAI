namespace Rebel.Web.Models;

public sealed record BeerNoMatchRecovery(
    string Reply,
    IReadOnlyList<BeerChatFollowUp> FollowUps);
