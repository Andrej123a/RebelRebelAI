using System.ComponentModel.DataAnnotations;

namespace Rebel.Domain.Entities;

public class BeerGuideResponseContext
{
    public Guid Id { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    [Required]
    [StringLength(500)]
    public string RecommendedProductIds { get; set; } = string.Empty;

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
}
