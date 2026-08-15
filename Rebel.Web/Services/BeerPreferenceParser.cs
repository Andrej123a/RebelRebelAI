using System.Text.RegularExpressions;
using Rebel.Domain.Enums;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public class BeerPreferenceParser : IBeerPreferenceParser
{
    private static readonly IReadOnlyDictionary<string, string[]> Styles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ipa"] = ["ipa", "ipas"],
            ["lager"] = ["lager", "lagers"],
            ["pilsner"] = ["pils", "pilsner", "pilsners"],
            ["stout"] = ["stout", "stouts"],
            ["porter"] = ["porter", "porters"],
            ["tripel"] = ["tripel", "tripels"],
            ["sour"] = ["sour", "sours", "gose", "lambic"],
            ["wheat"] = ["wheat", "weissbier", "weizen", "witbier"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> Flavours =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["citrus"] = ["citrus", "citrussy", "citrusy"],
            ["grapefruit"] = ["grapefruit", "pomelo"],
            ["lemon"] = ["lemon"],
            ["lime"] = ["lime"],
            ["orange"] = ["orange"],
            ["yuzu"] = ["yuzu"],
            ["tropical"] = ["tropical", "passionfruit"],
            ["mango"] = ["mango"],
            ["berry"] = ["berry", "berries", "raspberry", "strawberry"],
            ["cherry"] = ["cherry"],
            ["coffee"] = ["coffee", "mocaccino"],
            ["chocolate"] = ["chocolate", "cocoa"],
            ["caramel"] = ["caramel"],
            ["pine"] = ["pine", "resin", "resiny"],
            ["roasty"] = ["roast", "roasted", "roasty"],
            ["smoky"] = ["smoke", "smoky", "smoked"],
            ["banana"] = ["banana"],
            ["honey"] = ["honey"],
            ["crisp"] = ["crisp", "clean"],
            ["refreshing"] = ["refreshing", "refreshment", "refresh", "summery"],
            ["malty"] = ["malt", "malty"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> Origins =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["germany"] = ["german", "germany"],
            ["belgium"] = ["belgian", "belgium"],
            ["czechia"] = ["czech", "czechia"],
            ["hungary"] = ["hungarian", "hungary"],
            ["local"] = ["local", "skopje", "macedonian"]
        };

    private static readonly string[] Foods =
    [
        "burger", "pizza", "wings", "chicken", "sausage", "fries",
        "spicy", "smoked", "dessert", "cheese", "salad"
    ];

    public BeerPreferenceFingerprint Parse(
        string query,
        BeerFeedbackReason? correctionReason = null)
    {
        var normalized = query.ToLowerInvariant();
        var style = FindCanonical(normalized, Styles);
        if (!string.IsNullOrWhiteSpace(style) &&
            Regex.IsMatch(
                normalized,
                $@"\b(?:no|not|without)\s+(?:a\s+)?(?:{Regex.Escape(style)}|{Regex.Escape(style)}s)\b",
                RegexOptions.IgnoreCase))
        {
            style = null;
        }
        var flavours = Flavours
            .Where(pair => pair.Value.Any(value => ContainsWord(normalized, value)))
            .Select(pair => pair.Key)
            .ToList();
        var origin = FindCanonical(normalized, Origins);
        var strength = ContainsAny(normalized, "strong", "imperial", "high alcohol", "high abv", "highest alcohol", "highest abv", "over 7", "above 7")
            ? "high"
            : ContainsAny(normalized, "weak", "light alcohol", "low alcohol", "under 5", "below 5", "session")
                ? "low"
                : null;
        var bitterness = Regex.IsMatch(
                normalized,
                @"\b(not|no|less|without)\s+(too\s+)?bitter\b")
            ? "low"
            : ContainsAny(normalized, "bitter", "hoppy")
                ? "high"
                : null;
        var sweetness = Regex.IsMatch(
                normalized,
                @"\b(not|no|less|without)\s+(too\s+)?sweet\b")
            ? "low"
            : ContainsWord(normalized, "sweet")
                ? "high"
                : null;
        var food = Foods.FirstOrDefault(value => ContainsWord(normalized, value));

        switch (correctionReason)
        {
            case BeerFeedbackReason.TooBitter:
                bitterness = "low";
                break;
            case BeerFeedbackReason.TooSweet:
                sweetness = "low";
                break;
            case BeerFeedbackReason.TooStrong:
                strength = "low";
                break;
            case BeerFeedbackReason.TooWeak:
                strength = "high";
                break;
        }

        return new BeerPreferenceFingerprint(
            style,
            flavours,
            origin,
            strength,
            bitterness,
            sweetness,
            food);
    }

    private static string? FindCanonical(
        string query,
        IReadOnlyDictionary<string, string[]> values) =>
        values.FirstOrDefault(pair =>
            pair.Value.Any(value => ContainsWord(query, value))).Key;

    private static bool ContainsAny(string query, params string[] values) =>
        values.Any(value => query.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsWord(string query, string value) =>
        Regex.IsMatch(
            query,
            $@"\b{Regex.Escape(value)}\b",
            RegexOptions.IgnoreCase);
}
