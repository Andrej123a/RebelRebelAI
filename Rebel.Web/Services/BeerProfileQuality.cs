using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Rebel.Web.Services;

public static class BeerProfileQuality
{
    public static bool IsBeer(Product product) =>
        product.Category?.Type == CategoryType.Beer ||
        product.Category?.Name?.Contains("beer", StringComparison.OrdinalIgnoreCase) == true;

    public static int CompletionPercent(Product product)
    {
        var checks = ProfileChecks(product);
        var completed = checks.Count(check => check.Complete);
        return checks.Count == 0
            ? 0
            : (int)Math.Round(completed / (double)checks.Count * 100);
    }

    public static IReadOnlyList<string> MissingFields(Product product) =>
        ProfileChecks(product)
            .Where(check => !check.Complete)
            .Select(check => check.Label)
            .ToList();

    public static IReadOnlyList<string> ReviewNotes(Product product)
    {
        var notes = new List<string>();

        if (!product.IsAvailable)
        {
            notes.Add("Unavailable: hidden from guest recommendations");
        }

        if (!product.AlcoholByVolume.HasValue && AlcoholByVolume(product).HasValue)
        {
            notes.Add("Confirm the ABV currently read from the description");
        }

        return notes;
    }

    public static bool IsReadyForGuide(Product product) =>
        product.IsAvailable &&
        MissingFields(product).Count == 0 &&
        ReviewNotes(product).Count == 0;

    public static decimal? AlcoholByVolume(Product product)
    {
        if (product.AlcoholByVolume.HasValue)
        {
            return product.AlcoholByVolume;
        }

        var source = string.Join(' ', new[] { product.Description, product.Name }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var match = Regex.Match(
            source,
            @"(?:(?<valueAfter>\d{1,2}(?:[.,]\d+)?)\s*%?\s*(?:ABV\b|\bALC\b)|\bABV\s*[:\-]?\s*(?<valueBefore>\d{1,2}(?:[.,]\d+)?)\s*%?)",
            RegexOptions.IgnoreCase);

        var value = match.Groups["valueAfter"].Success
            ? match.Groups["valueAfter"].Value
            : match.Groups["valueBefore"].Value;

        if (!match.Success ||
            !decimal.TryParse(
                value.Replace(',', '.'),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed is <= 0 or > 20)
        {
            return null;
        }

        return parsed;
    }

    private static IReadOnlyList<(string Label, bool Complete)> ProfileChecks(Product product)
    {
        var checks = new List<(string Label, bool Complete)>
        {
            ("country", !string.IsNullOrWhiteSpace(product.OriginCountry)),
            ("style", !string.IsNullOrWhiteSpace(product.BeerStyle)),
            ("ABV", AlcoholByVolume(product).HasValue),
            ("body", product.BodyLevel.HasValue),
            ("bitterness", product.BitternessLevel.HasValue),
            ("sweetness", product.SweetnessLevel.HasValue),
            ("flavour notes", !string.IsNullOrWhiteSpace(product.FlavorNotes)),
            ("food pairings", !string.IsNullOrWhiteSpace(product.PairingTags))
        };

        var style = product.BeerStyle ?? string.Empty;
        if (style.Contains("sour", StringComparison.OrdinalIgnoreCase) ||
            style.Contains("gose", StringComparison.OrdinalIgnoreCase) ||
            style.Contains("lambic", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add(("acidity", product.AcidityLevel.HasValue));
        }

        return checks;
    }
}
