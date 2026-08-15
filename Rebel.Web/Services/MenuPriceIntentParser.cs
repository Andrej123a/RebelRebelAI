using System.Globalization;
using System.Text.RegularExpressions;
using Rebel.Domain.Enums;

namespace Rebel.Web.Services;

public sealed record MenuPriceIntent(
    decimal? Minimum,
    decimal? Maximum,
    decimal? Target,
    string? Tier,
    bool Cheapest,
    bool MostExpensive)
{
    public bool HasPreference =>
        Minimum.HasValue || Maximum.HasValue || Target.HasValue ||
        Tier != null || Cheapest || MostExpensive;

    public (decimal? Minimum, decimal? Maximum) Bounds(CategoryType type) =>
        Tier switch
        {
            "budget" => (null, type == CategoryType.Beer ? 300m : 250m),
            "mid-range" => type == CategoryType.Beer
                ? (301m, 450m)
                : (251m, 400m),
            "premium" => (type == CategoryType.Beer ? 451m : 401m, null),
            _ => (Minimum, Maximum)
        };
}

public static partial class MenuPriceIntentParser
{
    public static MenuPriceIntent Parse(string query)
    {
        var range = RangePattern().Match(query);
        if (range.Success &&
            TryDecimal(range.Groups[1].Value, out var first) &&
            TryDecimal(range.Groups[2].Value, out var second) &&
            LooksLikePrice(range.Groups[3].Success, first, second))
        {
            return new MenuPriceIntent(
                Math.Min(first, second),
                Math.Max(first, second),
                null,
                null,
                false,
                false);
        }

        var target = TargetPattern().Match(query);
        if (target.Success &&
            TryDecimal(target.Groups[1].Value, out var targetPrice) &&
            LooksLikePrice(target.Groups[2].Success, targetPrice))
        {
            return new MenuPriceIntent(null, null, targetPrice, null, false, false);
        }

        var limit = LimitPattern().Match(query);
        if (limit.Success &&
            TryDecimal(limit.Groups[2].Value, out var limitPrice) &&
            LooksLikePrice(limit.Groups[3].Success, limitPrice))
        {
            var lowerBound = limit.Groups[1].Value is "over" or "above" or "more";
            return lowerBound
                ? new MenuPriceIntent(limitPrice, null, null, null, false, false)
                : new MenuPriceIntent(null, limitPrice, null, null, false, false);
        }

        var tier = TierPattern().Match(query);
        var tierName = tier.Success
            ? tier.Groups[1].Value.Length switch
            {
                1 => "budget",
                2 => "mid-range",
                _ => "premium"
            }
            : WordTier(query);

        return new MenuPriceIntent(
            null,
            null,
            null,
            tierName,
            CheapestPattern().IsMatch(query),
            MostExpensivePattern().IsMatch(query));
    }

    public static bool IsPriceRequest(string query) => Parse(query).HasPreference;

    private static string? WordTier(string query)
    {
        if (BudgetPattern().IsMatch(query) && !CheapestPattern().IsMatch(query))
            return "budget";
        if (MidRangePattern().IsMatch(query)) return "mid-range";
        if (PremiumPattern().IsMatch(query) && !MostExpensivePattern().IsMatch(query))
            return "premium";
        return null;
    }

    private static bool TryDecimal(string value, out decimal result) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

    private static bool LooksLikePrice(bool hasCurrency, params decimal[] values) =>
        hasCurrency || values.All(value => value >= 20);

    [GeneratedRegex(@"\b(?:(?:between|from)\s*)?([0-9]+(?:\.[0-9]+)?)\s*(?:mkd|denars?)?\s*(?:and|to|-)\s*([0-9]+(?:\.[0-9]+)?)\s*(mkd|denars?)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex RangePattern();

    [GeneratedRegex(@"\b(?:around|about|roughly|approximately|close\s+to|near|at)\s*([0-9]+(?:\.[0-9]+)?)\s*(mkd|denars?)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex TargetPattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(mkd|denars?)?\b", RegexOptions.IgnoreCase)]
    private static partial Regex LimitPattern();

    [GeneratedRegex(@"(?<![\d$])(\${1,3})(?![\d$])")]
    private static partial Regex TierPattern();

    [GeneratedRegex(@"\b(?:budget|cheap|affordable|low[- ]?cost)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BudgetPattern();

    [GeneratedRegex(@"\b(?:mid[- ]?range|moderate(?:ly)? priced|middle price)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MidRangePattern();

    [GeneratedRegex(@"\b(?:premium|pricey|high[- ]?end|top[- ]?shelf)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PremiumPattern();

    [GeneratedRegex(@"\b(?:cheapest|cheaper|lowest\s+price|least\s+expensive)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CheapestPattern();

    [GeneratedRegex(@"\b(?:most\s+expensive|more\s+expensive|priciest|highest[- ]?price|costliest)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MostExpensivePattern();
}
