using System.ComponentModel.DataAnnotations;
using Rebel.Domain.Enums;

namespace Rebel.Domain.Entities;

public class BeerGuideFeedback
{
    public Guid Id { get; set; }

    public Guid ResponseId { get; set; }

    [Required]
    [StringLength(64)]
    public string AnonymousSessionHash { get; set; } = string.Empty;

    public bool IsPositive { get; set; }

    public BeerFeedbackReason? Reason { get; set; }

    [StringLength(40)]
    public string? RequestedStyle { get; set; }

    [StringLength(160)]
    public string? FlavourTags { get; set; }

    [StringLength(40)]
    public string? RequestedOrigin { get; set; }

    [StringLength(20)]
    public string? StrengthPreference { get; set; }

    [StringLength(20)]
    public string? BitternessPreference { get; set; }

    [StringLength(20)]
    public string? SweetnessPreference { get; set; }

    [StringLength(40)]
    public string? FoodPairing { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Guid ProductId { get; set; }

    public Product? Product { get; set; }
}
