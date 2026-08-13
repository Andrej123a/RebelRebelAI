using System.Text.RegularExpressions;
using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public sealed partial class BeerProfileSuggestionService : IBeerProfileSuggestionService
{
    private static readonly IReadOnlyList<(string Value, string Pattern)> Styles =
    [
        ("West Coast IPA", @"\bwest\s+coast\s+ipa\b"),
        ("New England IPA", @"\b(?:new\s+england|neipa)\b"),
        ("Red IPA", @"\bred\s+ipa\b"),
        ("Double IPA", @"\b(?:double\s+ipa|dipa)\b"),
        ("Czech pilsner", @"\bczech\s+pils(?:ner)?\b"),
        ("Belgian tripel", @"\bbelgian\s+tripel\b"),
        ("Wheat beer", @"\b(?:wheat\s+beer|weissbier|weizen|witbier)\b"),
        ("Pale ale", @"\bpale\s+ale\b"),
        ("IPA", @"\bipa\b"),
        ("Pilsner", @"\bpils(?:ner)?\b"),
        ("Lager", @"\blager\b"),
        ("Stout", @"\bstout\b"),
        ("Porter", @"\bporter\b"),
        ("Tripel", @"\btripel\b"),
        ("Sour", @"\b(?:sour|gose|lambic)\b")
    ];

    private static readonly IReadOnlyDictionary<string, string[]> Countries =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Belgium"] = ["belgium", "belgian"],
            ["Germany"] = ["germany", "german"],
            ["Czechia"] = ["czechia", "czech"],
            ["Hungary"] = ["hungary", "hungarian"],
            ["North Macedonia"] = ["north macedonia", "macedonian", "skopje"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> Flavours =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["grapefruit"] = ["grapefruit", "pomelo"],
            ["citrus"] = ["citrus", "citrussy", "citrusy"],
            ["lemon"] = ["lemon"],
            ["lime"] = ["lime"],
            ["orange"] = ["orange"],
            ["mango"] = ["mango"],
            ["tropical"] = ["tropical", "passionfruit"],
            ["cherry"] = ["cherry"],
            ["berry"] = ["berry", "berries", "raspberry", "strawberry"],
            ["coffee"] = ["coffee"],
            ["chocolate"] = ["chocolate", "cocoa"],
            ["caramel"] = ["caramel"],
            ["pine"] = ["pine", "resin", "resiny"],
            ["roasted malt"] = ["roast", "roasted", "roasty"],
            ["smoke"] = ["smoke", "smoky", "smoked"],
            ["banana"] = ["banana"],
            ["honey"] = ["honey"],
            ["crisp"] = ["crisp", "clean"]
        };

    private static readonly IReadOnlyDictionary<string, string[]> Foods =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["burger"] = ["burger", "burgers"],
            ["pizza"] = ["pizza"],
            ["spicy food"] = ["spicy food", "spicy dishes"],
            ["smoked meat"] = ["smoked meat", "smoked meats"],
            ["chicken"] = ["chicken"],
            ["cheese"] = ["cheese"],
            ["dessert"] = ["dessert", "desserts"],
            ["salad"] = ["salad", "salads"]
        };

    public IReadOnlyList<BeerProfileSuggestionViewModel> Suggest(Product beer)
    {
        var source = string.Join(". ", new[] { beer.Name, beer.Description }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var evidence = source.Length <= 180 ? source : source[..177].TrimEnd() + "...";
        var suggestions = new List<BeerProfileSuggestionViewModel>();

        if (!beer.AlcoholByVolume.HasValue && BeerProfileQuality.AlcoholByVolume(beer) is { } abv)
        {
            suggestions.Add(Suggestion(
                nameof(Product.AlcoholByVolume),
                "ABV",
                abv.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                evidence));
        }

        if (string.IsNullOrWhiteSpace(beer.BeerStyle))
        {
            var style = Styles.FirstOrDefault(item =>
                Regex.IsMatch(source, item.Pattern, RegexOptions.IgnoreCase)).Value;
            AddText(suggestions, nameof(Product.BeerStyle), "Style", style, evidence);
        }

        if (string.IsNullOrWhiteSpace(beer.OriginCountry))
        {
            var country = Countries.FirstOrDefault(pair =>
                pair.Value.Any(term => ContainsWord(source, term))).Key;
            AddText(suggestions, nameof(Product.OriginCountry), "Country", country, evidence);
        }

        if (string.IsNullOrWhiteSpace(beer.FlavorNotes))
        {
            var flavours = Flavours
                .Where(pair => pair.Value.Any(term => ContainsWord(source, term)))
                .Select(pair => pair.Key)
                .Take(5)
                .ToList();
            AddText(
                suggestions,
                nameof(Product.FlavorNotes),
                "Flavour notes",
                flavours.Count > 0 ? string.Join(", ", flavours) : null,
                evidence);
        }

        if (string.IsNullOrWhiteSpace(beer.PairingTags) && PairingCuePattern().IsMatch(source))
        {
            var pairings = Foods
                .Where(pair => pair.Value.Any(term => ContainsWord(source, term)))
                .Select(pair => pair.Key)
                .Take(4)
                .ToList();
            AddText(
                suggestions,
                nameof(Product.PairingTags),
                "Food pairings",
                pairings.Count > 0 ? string.Join(", ", pairings) : null,
                evidence);
        }

        return suggestions;
    }

    private static void AddText(
        ICollection<BeerProfileSuggestionViewModel> suggestions,
        string field,
        string label,
        string? value,
        string evidence)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            suggestions.Add(Suggestion(field, label, value, evidence));
        }
    }

    private static BeerProfileSuggestionViewModel Suggestion(
        string field,
        string label,
        string value,
        string evidence) => new()
    {
        Field = field,
        Label = label,
        Value = value,
        Evidence = evidence
    };

    private static bool ContainsWord(string source, string value) =>
        Regex.IsMatch(
            source,
            $@"\b{Regex.Escape(value)}\b",
            RegexOptions.IgnoreCase);

    [GeneratedRegex(@"\b(pair(?:s|ed|ing)?\s+(?:well\s+)?with|goes\s+(?:well\s+)?with|serve\s+with)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PairingCuePattern();
}
