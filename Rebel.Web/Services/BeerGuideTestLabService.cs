using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public sealed class BeerGuideTestLabService : IBeerGuideTestLabService
{
    private readonly IBeerCatalogMatcher _matcher;

    public BeerGuideTestLabService(IBeerCatalogMatcher matcher)
    {
        _matcher = matcher;
    }

    public IReadOnlyList<BeerGuideLabScenarioDefinition> Scenarios { get; } =
    [
        Scenario("citrus-ipa", "Show me three citrussy IPAs.", "IPA style and citrus-family flavour", 3,
            styles: ["ipa"], flavours: ["citrus", "grapefruit", "lemon", "lime", "orange"]),
        Scenario("grapefruit-ipa", "I want two IPAs with grapefruit aroma.", "IPA style and grapefruit profile", 2,
            styles: ["ipa"], flavours: ["grapefruit"]),
        Scenario("czech-pilsner", "Show me two Czech pilsners.", "Czech origin and pilsner style", 2,
            styles: ["pilsner", "pils"], origins: ["czechia", "czech"], allowNoMatches: true),
        Scenario("german-wheat", "I want two German wheat beers.", "German origin and wheat style", 2,
            styles: ["wheat", "weissbier", "weizen"], origins: ["germany", "german"], allowNoMatches: true),
        Scenario("dark-roasty", "Give me three dark, roasty beers.", "Stout or porter with roasted flavours", 3,
            styles: ["stout", "porter", "black ipa"], flavours: ["roast", "coffee", "chocolate"]),
        Scenario("not-bitter", "Give me two beers that are not bitter.", "Bitterness level 2 or lower", 2,
            maximumBitterness: 2),
        Scenario("not-sweet", "Give me two beers that are not sweet.", "Sweetness level 2 or lower", 2,
            maximumSweetness: 2),
        Scenario("under-five", "Show me three beers under 5% ABV.", "Every result below 5% ABV", 3,
            maximumAbvExclusive: 5m),
        Scenario("over-seven", "Show me two beers over 7% ABV.", "Every result above 7% ABV", 2,
            minimumAbvExclusive: 7m),
        Scenario("strong-belgian", "I want two strong Belgian beers over 7%.", "Belgian origin and ABV above 7%", 2,
            origins: ["belgium", "belgian"], minimumAbvExclusive: 7m, allowNoMatches: true),
        Scenario("berry-sour", "Show me two berry or cherry sour beers.", "Sour style and berry-family flavour", 2,
            styles: ["sour", "gose", "lambic"], flavours: ["berry", "cherry"]),
        Scenario("tropical", "Give me three tropical or mango beers.", "Tropical flavour profile", 3,
            flavours: ["tropical", "mango", "passionfruit"]),
        Scenario("burger-pairing", "Give me three beers for a burger.", "Burger food-pairing tag", 3,
            pairings: ["burger"]),
        Scenario("pizza-pairing", "Give me three beers for pizza.", "Pizza food-pairing tag", 3,
            pairings: ["pizza"]),
        Scenario("sausage-pairing", "Give me two beers for a sausage.", "Sausage food-pairing tag", 2,
            pairings: ["sausage"])
    ];

    public IReadOnlyList<AdminBeerGuideLabScenarioViewModel> RunDeterministic(
        IReadOnlyCollection<Product> beers) =>
        Scenarios
            .Select(scenario => Evaluate(
                scenario,
                _matcher.Shortlist(
                    scenario.Prompt,
                    beers,
                    scenario.RequestedCount)))
            .ToList();

    public AdminBeerGuideLabScenarioViewModel Evaluate(
        BeerGuideLabScenarioDefinition scenario,
        IReadOnlyList<Product> matches,
        bool isAiTest = false,
        bool usedAi = false,
        string? reply = null)
    {
        var issues = new List<AdminBeerGuideLabIssueViewModel>();

        if (matches.Count == 0)
        {
            issues.Add(Issue(
                scenario.AllowNoMatches ? "warning" : "fail",
                scenario.AllowNoMatches
                    ? "No exact catalogue match exists; the guide should explain that honestly."
                    : "No matching beer was returned."));
        }
        else if (matches.Count < scenario.RequestedCount)
        {
            issues.Add(Issue(
                "warning",
                $"Requested {scenario.RequestedCount}, but only {matches.Count} matched."));
        }

        foreach (var beer in matches)
        {
            if (!beer.IsAvailable)
            {
                issues.Add(Issue(
                    "fail",
                    "Unavailable beer was returned as a recommendation.",
                    beer));
            }

            CheckText(issues, beer, "style", beer.BeerStyle, scenario.Styles);
            CheckText(issues, beer, "flavour notes", beer.FlavorNotes, scenario.Flavours);
            CheckText(issues, beer, "country", beer.OriginCountry, scenario.Origins);
            CheckText(issues, beer, "food pairings", beer.PairingTags, scenario.Pairings);
            CheckAbv(issues, beer, scenario);
            CheckScale(
                issues,
                beer,
                "bitterness",
                beer.BitternessLevel,
                scenario.MaximumBitterness);
            CheckScale(
                issues,
                beer,
                "sweetness",
                beer.SweetnessLevel,
                scenario.MaximumSweetness);
        }

        var status = issues.Any(issue => issue.Severity == "fail")
            ? "fail"
            : issues.Count > 0
                ? "warning"
                : "pass";

        return new AdminBeerGuideLabScenarioViewModel
        {
            Key = scenario.Key,
            Prompt = scenario.Prompt,
            Checks = scenario.Checks,
            RequestedCount = scenario.RequestedCount,
            Status = status,
            IsAiTest = isAiTest,
            UsedAi = usedAi,
            Reply = reply,
            Beers = matches,
            Issues = issues
        };
    }

    private static void CheckText(
        ICollection<AdminBeerGuideLabIssueViewModel> issues,
        Product beer,
        string label,
        string? value,
        IReadOnlyList<string> expected)
    {
        if (expected.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(Issue("warning", $"Cannot verify {label}; the profile is empty.", beer));
            return;
        }

        if (!expected.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add(Issue(
                "fail",
                $"{label} does not match: expected {string.Join(" or ", expected)}.",
                beer));
        }
    }

    private static void CheckAbv(
        ICollection<AdminBeerGuideLabIssueViewModel> issues,
        Product beer,
        BeerGuideLabScenarioDefinition scenario)
    {
        if (!scenario.MinimumAbvExclusive.HasValue &&
            !scenario.MaximumAbvExclusive.HasValue)
        {
            return;
        }

        var abv = BeerProfileQuality.AlcoholByVolume(beer);
        if (!abv.HasValue)
        {
            issues.Add(Issue("warning", "Cannot verify ABV; the profile is empty.", beer));
            return;
        }

        if (scenario.MinimumAbvExclusive.HasValue &&
            abv.Value <= scenario.MinimumAbvExclusive.Value)
        {
            issues.Add(Issue(
                "fail",
                $"ABV is {abv.Value:0.#}%; expected above {scenario.MinimumAbvExclusive.Value:0.#}%.",
                beer));
        }

        if (scenario.MaximumAbvExclusive.HasValue &&
            abv.Value >= scenario.MaximumAbvExclusive.Value)
        {
            issues.Add(Issue(
                "fail",
                $"ABV is {abv.Value:0.#}%; expected below {scenario.MaximumAbvExclusive.Value:0.#}%.",
                beer));
        }
    }

    private static void CheckScale(
        ICollection<AdminBeerGuideLabIssueViewModel> issues,
        Product beer,
        string label,
        int? value,
        int? maximum)
    {
        if (!maximum.HasValue)
        {
            return;
        }

        if (!value.HasValue)
        {
            issues.Add(Issue("warning", $"Cannot verify {label}; the profile is empty.", beer));
            return;
        }

        if (value.Value > maximum.Value)
        {
            issues.Add(Issue(
                "fail",
                $"{label} is {value.Value}/5; expected {maximum.Value}/5 or lower.",
                beer));
        }
    }

    private static AdminBeerGuideLabIssueViewModel Issue(
        string severity,
        string message,
        Product? beer = null) => new()
    {
        Severity = severity,
        Message = message,
        ProductId = beer?.Id,
        ProductName = beer?.Name
    };

    private static BeerGuideLabScenarioDefinition Scenario(
        string key,
        string prompt,
        string checks,
        int requestedCount,
        IReadOnlyList<string>? styles = null,
        IReadOnlyList<string>? flavours = null,
        IReadOnlyList<string>? origins = null,
        IReadOnlyList<string>? pairings = null,
        decimal? minimumAbvExclusive = null,
        decimal? maximumAbvExclusive = null,
        int? maximumBitterness = null,
        int? maximumSweetness = null,
        bool allowNoMatches = false) => new()
    {
        Key = key,
        Prompt = prompt,
        Checks = checks,
        RequestedCount = requestedCount,
        Styles = styles ?? [],
        Flavours = flavours ?? [],
        Origins = origins ?? [],
        Pairings = pairings ?? [],
        MinimumAbvExclusive = minimumAbvExclusive,
        MaximumAbvExclusive = maximumAbvExclusive,
        MaximumBitterness = maximumBitterness,
        MaximumSweetness = maximumSweetness,
        AllowNoMatches = allowNoMatches
    };
}
