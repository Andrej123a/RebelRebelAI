using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Entities;

namespace Rebel.Web.Models;

public class BeerGuideRequest
{
    public string Mode { get; set; } = "taste";

    public string? Taste { get; set; }

    [Range(1, 5)]
    public int? Intensity { get; set; }

    public string Adventure { get; set; } = "curious";

    public Guid? FoodProductId { get; set; }
}

public class BeerGuideRecommendation
{
    public required Product Beer { get; init; }

    public required string Label { get; init; }

    public required string Reason { get; init; }

    public int Score { get; init; }
}

public class BeerGuideViewModel
{
    public BeerGuideRequest Request { get; set; } = new();

    public List<Product> Foods { get; set; } = new();

    public List<BeerGuideRecommendation> Recommendations { get; set; } = new();

    public int AvailableBeerCount { get; set; }

    public int AvailableFoodCount { get; set; }

    public bool HasSearched { get; set; }

    public bool AiIsConfigured { get; set; }

    public bool AiWasUsed { get; set; }

    public string? InitialChatPrompt { get; set; }
}
