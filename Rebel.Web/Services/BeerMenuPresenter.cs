using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public static partial class BeerMenuPresenter
{
    private const string DarkInk = "#0E0807";
    private const string Ink = "#1A0F0D";
    private const string LightInk = "#FFF1DC";
    private const string SoldOutBackground = "#2A1310";

    // Every style family floods the stage with one of the site's colours:
    // mostly Ziggy red and gold, with light blue kept for the rare wildcard.
    // The glow is the second colour, used on the disc, name shadow and sliders.
    public static readonly BeerMenuFamily Hoppy =
        new("hoppy", "Hoppy & Hazy", "#BB150B", LightInk, "#F2C23A");

    public static readonly BeerMenuFamily Crisp =
        new("crisp", "Crisp & Golden", "#F2C23A", Ink, "#BB150B");

    public static readonly BeerMenuFamily Sour =
        new("sour", "Sour & Fruity", "#E2401A", "#120806", "#FFF1DC");

    public static readonly BeerMenuFamily Dark =
        new("dark", "Dark & Roasty", Ink, LightInk, "#F2C23A");

    public static readonly BeerMenuFamily Amber =
        new("amber", "Amber & Malty", "#7D0D06", LightInk, "#F2C23A");

    public static readonly BeerMenuFamily Belgian =
        new("belgian", "Belgian & Wheat", "#F1E8D6", Ink, "#BB150B");

    public static readonly BeerMenuFamily Wildcard =
        new("wildcard", "Wildcards", "#9AD3F0", Ink, "#BB150B");

    private static readonly IReadOnlyList<BeerMenuFamily> FamilyOrder =
        [Hoppy, Crisp, Sour, Dark, Amber, Belgian, Wildcard];

    // Checked in order: a "black IPA" is dark and a "fruited IPA" is sour,
    // a "red IPA" stays hoppy and an "amber lager" is amber.
    private static readonly (BeerMenuFamily Family, string[] Tokens, string[] Phrases)[] FamilyRules =
    [
        (Sour,
            ["sour", "gose", "lambic", "kriek", "gueuze", "geuze", "framboise",
             "berliner", "wild", "fruit", "fruited", "radler", "cider", "flanders", "brett"],
            []),
        (Dark,
            ["stout", "porter", "schwarz", "schwarzbier", "dunkel", "dunkelweizen",
             "black", "baltic"],
            []),
        (Hoppy,
            ["ipa", "neipa", "dipa", "tipa", "iipa", "apa", "hazy", "hoppy"],
            ["pale ale", "india pale"]),
        (Belgian,
            ["tripel", "dubbel", "quad", "quadrupel", "saison", "abbey", "abbaye",
             "trappist", "belgian", "wit", "witbier", "blanche", "weiss", "weissbier",
             "weizen", "hefeweizen", "hefe", "wheat", "farmhouse"],
            []),
        (Amber,
            ["amber", "red", "brown", "bitter", "esb", "marzen", "maerzen", "vienna",
             "scotch", "bock", "doppelbock", "eisbock", "barleywine", "alt", "altbier",
             "rauchbier", "oktoberfest"],
            ["barley wine"]),
        (Crisp,
            ["lager", "pils", "pilsner", "pilsener", "helles", "kolsch", "koelsch",
             "blonde", "blond", "golden", "export", "festbier", "light"],
            [])
    ];

    public static BeerMenuViewModel Build(IEnumerable<Product> products)
    {
        var beers = products
            .Where(product =>
                !product.IsDeleted &&
                product.Category is { IsDeleted: false, Type: CategoryType.Beer })
            .ToList();

        var groups = beers
            .GroupBy(product => product.Category!.Name)
            .OrderBy(group => group.Key, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var usedAnchors = new HashSet<string>(StringComparer.Ordinal);
        var sides = new List<BeerMenuSide>();
        var number = 1;
        string? previousVisual = null;

        foreach (var group in groups)
        {
            var tracks = new List<BeerMenuTrack>();

            var ordered = group
                .OrderByDescending(product => product.IsAvailable)
                .ThenByDescending(product => product.IsPopular)
                .ThenBy(product => product.Name, StringComparer.CurrentCultureIgnoreCase);

            foreach (var product in ordered)
            {
                var family = ClassifyFamily(product.BeerStyle, group.Key);

                // Two neighbours from the same family would leave the stage on one colour,
                // so every second one plays on plain black.
                var isInverted =
                    product.IsAvailable &&
                    previousVisual == family.Key;

                previousVisual = !product.IsAvailable
                    ? "sold-out"
                    : isInverted
                        ? $"inverted-{family.Key}"
                        : family.Key;

                tracks.Add(CreateTrack(product, number, group.Key, family, isInverted, usedAnchors));
                number++;
            }

            sides.Add(new BeerMenuSide(SideLabel(sides.Count), group.Key, tracks));
        }

        var families = FamilyOrder
            .Where(family => sides
                .SelectMany(side => side.Tracks)
                .Any(track => track.Family.Key == family.Key))
            .ToList();

        return new BeerMenuViewModel(
            sides,
            families,
            beers.Count,
            beers.Count(product => product.IsAvailable));
    }

    public static BeerMenuFamily ClassifyFamily(string? style, string? fallback = null)
    {
        return MatchFamily(style) ?? MatchFamily(fallback) ?? Wildcard;
    }

    public static int LevelPercent(int level)
    {
        return (Math.Clamp(level, 1, 5) - 1) * 25;
    }

    public static int StrengthPercent(decimal alcoholByVolume)
    {
        const decimal session = 3m;
        const decimal strong = 10m;

        var ratio = (alcoholByVolume - session) / (strong - session);

        return (int)Math.Round(Math.Clamp(ratio, 0m, 1m) * 100m, MidpointRounding.AwayFromZero);
    }

    public static string CreateAnchor(string name, int number, ISet<string> usedAnchors)
    {
        var slug = NonSlugCharacters()
            .Replace(RemoveDiacritics(name).ToLowerInvariant(), "-")
            .Trim('-');

        var baseAnchor = string.IsNullOrEmpty(slug)
            ? $"beer-{number:00}"
            : $"beer-{slug}";

        var anchor = baseAnchor;
        var suffix = 2;

        while (!usedAnchors.Add(anchor))
        {
            anchor = $"{baseAnchor}-{suffix}";
            suffix++;
        }

        return anchor;
    }

    public static IReadOnlyList<string> SplitList(string? value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static BeerMenuTrack CreateTrack(
        Product product,
        int number,
        string categoryName,
        BeerMenuFamily family,
        bool isInverted,
        ISet<string> usedAnchors)
    {
        var flavorNotes = SplitList(product.FlavorNotes, 6);
        var pairings = SplitList(product.PairingTags, 5);
        var name = product.Name.Trim();
        var style = NullIfBlank(product.BeerStyle);
        var origin = NullIfBlank(product.OriginCountry);
        var description = NullIfBlank(product.Description);

        var (stageBackground, stageInk, accent) = !product.IsAvailable
            ? (SoldOutBackground, LightInk, family.Glow)
            : isInverted
                ? (DarkInk, LightInk, family.Glow)
                : (family.Background, family.Ink, family.Glow);

        var searchText = string.Join(
                " ",
                new[] { name, style, origin, description, categoryName, family.Label }
                    .Concat(flavorNotes)
                    .Concat(pairings)
                    .Where(part => !string.IsNullOrWhiteSpace(part)))
            .ToLowerInvariant()
            .Replace("\"", string.Empty);

        return new BeerMenuTrack(
            product.Id,
            number,
            CreateAnchor(name, number, usedAnchors),
            name,
            style,
            origin,
            description,
            product.Price,
            product.AlcoholByVolume,
            NullIfBlank(product.ImageUrl),
            categoryName,
            product.IsAvailable,
            product.IsPopular,
            product.IsLimited,
            product.IsPromo,
            family,
            isInverted,
            stageBackground,
            stageInk,
            accent,
            flavorNotes,
            pairings,
            BuildTaste(product),
            searchText);
    }

    private static IReadOnlyList<BeerTasteScale> BuildTaste(Product product)
    {
        var scales = new List<BeerTasteScale>();

        AddLevel(scales, "body", "Body", "Light", "Full", product.BodyLevel);
        AddLevel(scales, "bitterness", "Bitterness", "Soft", "Bitter", product.BitternessLevel);
        AddLevel(scales, "sweetness", "Sweetness", "Dry", "Sweet", product.SweetnessLevel);
        AddLevel(scales, "acidity", "Acidity", "Clean", "Tart", product.AcidityLevel);

        if (product.AlcoholByVolume is { } abv and > 0)
        {
            scales.Add(new BeerTasteScale(
                "strength",
                "Strength",
                "Session",
                "Strong",
                StrengthPercent(abv),
                $"Strength: {abv.ToString("0.#", CultureInfo.InvariantCulture)}% ABV"));
        }

        return scales;
    }

    private static void AddLevel(
        List<BeerTasteScale> scales,
        string key,
        string label,
        string low,
        string high,
        int? level)
    {
        if (level is not { } value)
        {
            return;
        }

        var clamped = Math.Clamp(value, 1, 5);

        scales.Add(new BeerTasteScale(
            key,
            label,
            low,
            high,
            LevelPercent(clamped),
            $"{label}: {clamped} of 5, from {low.ToLowerInvariant()} to {high.ToLowerInvariant()}"));
    }

    private static BeerMenuFamily? MatchFamily(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var normalized = RemoveDiacritics(text).ToLowerInvariant();
        var words = WordSeparators()
            .Split(normalized)
            .Where(token => token.Length > 0)
            .ToList();

        // Category names are usually plural ("Stouts & Porters", "IPAs").
        var tokens = words
            .Concat(words
                .Where(word => word.Length > 3 && word.EndsWith('s'))
                .Select(word => word[..^1]))
            .ToHashSet(StringComparer.Ordinal);
        var spaced = string.Join(' ', WordSeparators().Split(normalized));

        foreach (var (family, familyTokens, phrases) in FamilyRules)
        {
            if (familyTokens.Any(tokens.Contains) ||
                phrases.Any(phrase => spaced.Contains(phrase, StringComparison.Ordinal)))
            {
                return family;
            }
        }

        return null;
    }

    private static string SideLabel(int index)
    {
        return index < 26
            ? $"Side {(char)('A' + index)}"
            : $"Side {index + 1}";
    }

    private static string RemoveDiacritics(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();

    [GeneratedRegex("[^\\p{L}\\p{N}]+")]
    private static partial Regex WordSeparators();
}
