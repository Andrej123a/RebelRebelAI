using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Entities;

namespace Rebel.Web.Models;

public class BeerChatRequest
{
    [Required]
    [StringLength(500, MinimumLength = 2)]
    public string Message { get; set; } = string.Empty;

    public List<BeerChatTurn> History { get; set; } = [];

    public BeerChatPreferenceState? Preferences { get; set; }

    public List<Guid> ExcludedBeerIds { get; set; } = [];

    public List<Guid> PreviousBeerIds { get; set; } = [];

    [StringLength(30)]
    public string? CorrectionReason { get; set; }
}

public sealed class BeerChatPreferenceState
{
    public string? ItemKind { get; set; }
    public string? Style { get; set; }
    public List<string> Flavours { get; set; } = [];
    public string? Origin { get; set; }
    public string? Strength { get; set; }
    public string? Bitterness { get; set; }
    public string? Sweetness { get; set; }
    public string? Heat { get; set; }
    public string? Saltiness { get; set; }
    public string? Richness { get; set; }
    public List<string> DietaryNeeds { get; set; } = [];
    public string? FoodPairing { get; set; }
    public decimal? MinimumAbv { get; set; }
    public decimal? MaximumAbv { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public decimal? TargetPrice { get; set; }
    public string? PriceTier { get; set; }
    public int? RequestedCount { get; set; }
    public int? RequestedBeerCount { get; set; }
    public int? RequestedFoodCount { get; set; }
    public decimal? TotalBudget { get; set; }
    public string? Sort { get; set; }
    public List<string> ExcludedStyles { get; set; } = [];
}

public sealed record BeerChatStateUpdate(
    BeerChatPreferenceState Preferences,
    string EffectiveQuery);

public class BeerChatTurn
{
    public string Role { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

public sealed record BeerChatMatch(Product Beer, string Reason);

public sealed record BeerChatResult(
    string Reply,
    IReadOnlyList<BeerChatMatch> Matches,
    bool UsedAi,
    IReadOnlyList<BeerChatFollowUp>? FollowUps = null);

public class BeerChatResponse
{
    public Guid? ResponseId { get; set; }

    public string Reply { get; set; } = string.Empty;

    public bool AiWasUsed { get; set; }

    public List<BeerChatBeerResponse> Beers { get; set; } = [];

    public List<BeerChatFollowUp> FollowUps { get; set; } = [];

    public BeerChatPreferenceState Preferences { get; set; } = new();
}

public sealed class BeerChatFollowUp
{
    public string Label { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string? GuestText { get; set; }
}

public class BeerChatFeedbackRequest
{
    public Guid ResponseId { get; set; }

    [Required]
    [StringLength(64, MinimumLength = 8)]
    public string SessionId { get; set; } = string.Empty;

    public bool? IsPositive { get; set; }

    [StringLength(30)]
    public string? Reason { get; set; }

    public List<Guid> ProductIds { get; set; } = [];
}

public class BeerChatBeerResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ItemType { get; set; } = "beer";

    public string? Category { get; set; }

    public string? ImageUrl { get; set; }

    public string? Style { get; set; }

    public string? Country { get; set; }

    public decimal? AlcoholByVolume { get; set; }

    public decimal Price { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int? BitternessLevel { get; set; }

    public int? SweetnessLevel { get; set; }

    public int? AcidityLevel { get; set; }

    public int? HeatLevel { get; set; }

    public int? SaltinessLevel { get; set; }

    public int? RichnessLevel { get; set; }

    public string? FlavorNotes { get; set; }

    public string? PairingTags { get; set; }
}
