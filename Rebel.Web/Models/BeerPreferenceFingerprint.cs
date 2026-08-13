namespace Rebel.Web.Models;

public sealed record BeerPreferenceFingerprint(
    string? Style,
    IReadOnlyList<string> Flavours,
    string? Origin,
    string? Strength,
    string? Bitterness,
    string? Sweetness,
    string? FoodPairing);
