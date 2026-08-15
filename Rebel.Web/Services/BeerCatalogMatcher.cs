using System.Globalization;
using System.Text.RegularExpressions;
using Rebel.Domain.Entities;

namespace Rebel.Web.Services;

public partial class BeerCatalogMatcher : IBeerCatalogMatcher
{
    private static readonly IReadOnlyDictionary<string, string[]> Aliases =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["citrussy"] = ["citrus", "grapefruit", "lemon", "lime", "orange"],
            ["citrusy"] = ["citrus", "grapefruit", "lemon", "lime", "orange"],
            ["grapefruit"] = ["grapefruit", "citrus", "pomelo"],
            ["grapefrut"] = ["grapefruit"],
            ["yuzu"] = ["yuzu", "citrus", "lemon", "lime"],
            ["refreshing"] = ["crisp", "light", "clean", "citrus", "yuzu", "lemon", "lime", "orange", "wheat", "pilsner", "tart"],
            ["refreshment"] = ["crisp", "light", "clean", "citrus", "yuzu", "lemon", "lime", "orange", "wheat", "pilsner", "tart"],
            ["refresh"] = ["crisp", "light", "clean", "citrus", "yuzu", "lemon", "lime", "orange", "wheat", "pilsner", "tart"],
            ["summery"] = ["crisp", "light", "clean", "citrus", "fruity", "wheat", "pilsner"],
            ["hoppy"] = ["hoppy", "hop", "pine", "resin", "ipa"],
            ["fruity"] = ["fruity", "fruit", "tropical", "berry", "citrus"],
            ["dark"] = ["dark", "stout", "porter", "roast", "coffee", "chocolate"],
            ["light"] = ["light", "lager", "pilsner", "crisp", "clean"],
            ["sour"] = ["sour", "tart", "gose", "lambic", "acidic"],
            ["sweet"] = ["sweet", "caramel", "malt", "malty", "honey"],
            ["strong"] = ["strong", "imperial", "tripel", "double"],
            ["wheat"] = ["wheat", "weissbier", "weizen", "witbier"],
            ["weissbier"] = ["weissbier", "wheat", "weizen"],
            ["weizen"] = ["weizen", "wheat", "weissbier"],
            ["witbier"] = ["witbier", "wheat"],
            ["pils"] = ["pils", "pilsner"],
            ["german"] = ["german", "germany"],
            ["belgian"] = ["belgian", "belgium"],
            ["czech"] = ["czech", "czechia"],
            ["hungarian"] = ["hungarian", "hungary"],
            ["food"] = ["food", "pairing"]
        };

    private static readonly HashSet<string> IgnoredWords = new(
        [
            "a", "an", "and", "aroma", "beer", "beers", "can", "do", "for",
            "give", "have", "i", "in", "is", "like", "me", "of", "please",
            "show", "some", "something", "that", "the", "to", "want", "we",
            "what", "with", "you", "only", "one", "two", "three", "four",
            "five", "six"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> PreferenceWords = new(
        [
            "ipa", "lager", "pilsner", "pils", "stout", "porter", "ale",
            "wheat", "weissbier", "weizen", "witbier", "sour", "gose", "lambic",
            "citrus", "citrussy", "citrusy", "grapefruit", "grapefrut", "yuzu", "lemon", "lime",
            "orange", "hoppy", "fruity", "tropical", "berry", "dark", "light",
            "refreshing", "refreshment", "refresh", "summery",
            "sweet", "bitter", "bitterness", "strong", "weak", "crisp", "malty",
            "coffee", "chocolate", "caramel", "spicy", "burger", "pizza", "food",
            "pairing", "sausage", "wing", "wings", "fries", "cheese", "chicken",
            "german", "germany", "belgian", "belgium", "czech",
            "czechia", "hungarian", "hungary", "local", "macedonian",
            "macedonia", "skopje"
        ],
        StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<Product> Shortlist(
        string query,
        IReadOnlyCollection<Product> beers,
        int limit,
        IReadOnlyDictionary<Guid, double>? feedbackScores = null,
        bool includeUnavailable = false)
    {
        var terms = QueryTerms(query);
        var eligible = includeUnavailable
            ? beers.ToList()
            : beers.Where(beer => beer.IsAvailable).ToList();
        var exclusionRestricted = ExcludeRejectedStyles(query, eligible);
        var styleRestricted = RestrictToExplicitStyle(query, exclusionRestricted);
        var preferenceRestricted = RestrictToExplicitFlavour(query, styleRestricted);
        var originRestricted = RestrictToExplicitOrigin(query, preferenceRestricted);
        var pairingRestricted = RestrictToExplicitPairing(query, originRestricted);
        var constraintRestricted = RestrictToExplicitConstraints(query, pairingRestricted);

        var ranked = constraintRestricted
            .Select(beer => new
            {
                Beer = beer,
                Score = Score(beer, query, terms) +
                    FeedbackWeight(beer.Id, feedbackScores)
            })
            .ToList();

        var ordered = HighestAbvPattern().IsMatch(query)
            ? ranked
                .OrderByDescending(item => BeerProfileQuality.AlcoholByVolume(item.Beer) ?? 0)
                .ThenByDescending(item => item.Score)
                .ThenBy(item => item.Beer.Name)
            : LowestAbvPattern().IsMatch(query)
                ? ranked
                    .Where(item => BeerProfileQuality.AlcoholByVolume(item.Beer).HasValue)
                    .OrderBy(item => BeerProfileQuality.AlcoholByVolume(item.Beer))
                    .ThenByDescending(item => item.Score)
                    .ThenBy(item => item.Beer.Name)
            : MostExpensivePattern().IsMatch(query)
            ? ranked
                .OrderByDescending(item => item.Beer.Price)
                .ThenByDescending(item => item.Score)
                .ThenBy(item => item.Beer.Name)
            : CheapestPattern().IsMatch(query)
                ? ranked
                    .OrderBy(item => item.Beer.Price)
                    .ThenByDescending(item => item.Score)
                    .ThenBy(item => item.Beer.Name)
                : RefreshingPattern().IsMatch(query)
                    ? ranked
                        .OrderByDescending(item =>
                            item.Score + RefreshmentProfileScore(item.Beer))
                        .ThenBy(item =>
                            BeerProfileQuality.AlcoholByVolume(item.Beer) ?? decimal.MaxValue)
                        .ThenBy(item => item.Beer.Name)
                : ranked
                    .OrderByDescending(item => item.Score)
                    .ThenByDescending(item => item.Beer.IsPopular)
                    .ThenBy(item => item.Beer.Name);

        return ordered
            .Take(Math.Max(0, limit))
            .Select(item => item.Beer)
            .ToList();
    }

    private static double RefreshmentProfileScore(Product beer)
    {
        var alcohol = BeerProfileQuality.AlcoholByVolume(beer);
        var score = alcohol.HasValue
            ? Math.Max(0, 8 - (double)alcohol.Value) * 12
            : 0;

        if (beer.BodyLevel.HasValue)
        {
            score += (6 - beer.BodyLevel.Value) * 6;
        }

        var profile = Normalize(string.Join(' ', new[]
        {
            beer.BeerStyle,
            beer.FlavorNotes,
            beer.Description
        }.Where(value => !string.IsNullOrWhiteSpace(value))));
        var refreshingCues = new[]
        {
            "refreshing", "crisp", "clean", "light", "citrus", "yuzu",
            "lemon", "lime", "wheat", "witbier", "pilsner", "tart"
        };

        score += refreshingCues.Count(cue => ContainsTerm(profile, cue)) * 5;
        return score;
    }

    private static IReadOnlyCollection<Product> RestrictToExplicitConstraints(
        string query,
        IReadOnlyCollection<Product> beers)
    {
        var normalizedQuery = Normalize(query);
        var restricted = beers;
        var abvMatch = AbvLimitPattern().Match(normalizedQuery);
        var priceMatch = PriceLimitPattern().Match(normalizedQuery);

        if (!priceMatch.Success &&
            abvMatch.Success &&
            decimal.TryParse(
                abvMatch.Groups[2].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var requestedAbv))
        {
            var lowerBound = abvMatch.Groups[1].Value is "over" or "above" or "more";
            var matching = restricted.Where(beer =>
                BeerProfileQuality.AlcoholByVolume(beer).HasValue &&
                (lowerBound
                    ? BeerProfileQuality.AlcoholByVolume(beer)!.Value > requestedAbv
                    : BeerProfileQuality.AlcoholByVolume(beer)!.Value < requestedAbv))
                .ToList();

            restricted = matching;
        }


        if (priceMatch.Success &&
            decimal.TryParse(
                priceMatch.Groups[2].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var requestedPrice))
        {
            var lowerBound = priceMatch.Groups[1].Value is "over" or "above" or "more";
            var matching = restricted.Where(beer =>
                lowerBound
                    ? beer.Price > requestedPrice
                    : beer.Price < requestedPrice)
                .ToList();

            restricted = matching;
        }

        if (NegativeBitterPattern().IsMatch(normalizedQuery))
        {
            var matching = restricted
                .Where(beer => beer.BitternessLevel is <= 2)
                .ToList();

            restricted = matching;
        }

        if (NegativeSweetPattern().IsMatch(normalizedQuery))
        {
            var matching = restricted
                .Where(beer => beer.SweetnessLevel is <= 2)
                .ToList();

            restricted = matching;
        }

        return restricted;
    }

    private static double FeedbackWeight(
        Guid beerId,
        IReadOnlyDictionary<Guid, double>? feedbackScores) =>
        feedbackScores?.TryGetValue(beerId, out var score) == true
            ? Math.Clamp(score, -4, 4) * 2
            : 0;

    private static IReadOnlyCollection<Product> RestrictToExplicitStyle(
        string query,
        IReadOnlyCollection<Product> beers)
    {
        var normalizedQuery = Normalize(query);
        var styleGroups = new[]
        {
            new[] { "ipa" },
            new[] { "lager" },
            new[] { "pils", "pilsner" },
            new[] { "stout" },
            new[] { "porter" },
            new[] { "tripel" },
            new[] { "sour", "gose", "lambic" },
            new[] { "wheat", "weissbier", "weizen", "witbier" }
        };

        var requestedGroup = styleGroups.FirstOrDefault(group =>
            group.Any(style => QueryContainsStyle(normalizedQuery, style)));

        if (requestedGroup == null)
        {
            return beers;
        }

        var matching = beers.Where(beer =>
        {
            var declaredStyle = Normalize(string.Join(' ', new[]
            {
                beer.BeerStyle,
                beer.Category?.Name,
                beer.Name,
                beer.Description
            }.Where(value => !string.IsNullOrWhiteSpace(value))));

            return requestedGroup.Any(style => ContainsTerm(declaredStyle, style));
        }).ToList();

        return matching;
    }

    private static IReadOnlyCollection<Product> ExcludeRejectedStyles(
        string query,
        IReadOnlyCollection<Product> beers)
    {
        var normalizedQuery = Normalize(query);
        var styleGroups = new[]
        {
            new[] { "ipa" },
            new[] { "lager" },
            new[] { "pils", "pilsner" },
            new[] { "stout" },
            new[] { "porter" },
            new[] { "tripel" },
            new[] { "sour", "gose", "lambic" },
            new[] { "wheat", "weissbier", "weizen", "witbier" }
        };

        var rejectedGroup = styleGroups.FirstOrDefault(group =>
            group.Any(style => Regex.IsMatch(
                normalizedQuery,
                $@"\b(?:no|not|without)\s+(?:a\s+)?{Regex.Escape(style)}s?\b",
                RegexOptions.IgnoreCase)));

        if (rejectedGroup == null)
        {
            return beers;
        }

        return beers.Where(beer =>
        {
            var declaredStyle = Normalize(string.Join(' ', new[]
            {
                beer.BeerStyle,
                beer.Name,
                beer.Description
            }.Where(value => !string.IsNullOrWhiteSpace(value))));

            return !rejectedGroup.Any(style => ContainsTerm(declaredStyle, style));
        }).ToList();
    }

    private static bool QueryContainsStyle(string query, string style) =>
        !Regex.IsMatch(
            query,
            $@"\b(?:no|not|without)\s+(?:a\s+)?{Regex.Escape(style)}s?\b",
            RegexOptions.IgnoreCase) &&
        (ContainsTerm(query, style) ||
         Regex.IsMatch(query, $@"\b{Regex.Escape(style)}s\b", RegexOptions.IgnoreCase));

    private static IReadOnlyCollection<Product> RestrictToExplicitFlavour(
        string query,
        IReadOnlyCollection<Product> beers)
    {
        var normalizedQuery = Normalize(query);
        var flavourGroups = new[]
        {
            new[] { "citrus", "citrussy", "citrusy", "grapefruit", "grapefrut", "yuzu", "lemon", "lime", "orange" },
            new[] { "tropical", "mango", "passionfruit" },
            new[] { "coffee", "chocolate", "roast", "roasted" },
            new[] { "caramel", "malty", "malt" },
            new[] { "berry", "cherry" },
            new[] { "pine", "resin" }
        };

        var requestedGroup = flavourGroups.FirstOrDefault(group =>
            group.Any(flavour => ContainsTerm(normalizedQuery, flavour)));

        if (requestedGroup == null)
        {
            return beers;
        }

        var specificallyRequested = requestedGroup
            .Where(flavour => ContainsTerm(normalizedQuery, flavour))
            .ToList();
        var broadFamilyRequest = specificallyRequested.Any(flavour =>
            flavour is "citrussy" or "citrusy");
        var typoRequest = specificallyRequested.Any(flavour => flavour is "grapefrut");
        var requestedFlavours = broadFamilyRequest
            ? requestedGroup.SelectMany(Expand)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
            : typoRequest
                ? specificallyRequested.SelectMany(Expand)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : specificallyRequested;

        var matching = beers.Where(beer =>
        {
            var recordedProfile = Normalize(string.Join(' ', new[]
            {
                beer.FlavorNotes,
                beer.Description
            }.Where(value => !string.IsNullOrWhiteSpace(value))));

            return requestedFlavours.Any(flavour =>
                ContainsTerm(recordedProfile, flavour));
        }).ToList();

        return matching;
    }

    private static IReadOnlyCollection<Product> RestrictToExplicitOrigin(
        string query,
        IReadOnlyCollection<Product> beers)
    {
        var normalizedQuery = Normalize(query);
        var origins = new[]
        {
            (Queries: new[] { "german", "germany" }, Profiles: new[] { "germany", "german" }),
            (Queries: new[] { "belgian", "belgium" }, Profiles: new[] { "belgium", "belgian" }),
            (Queries: new[] { "czech", "czechia" }, Profiles: new[] { "czechia", "czech" }),
            (Queries: new[] { "hungarian", "hungary" }, Profiles: new[] { "hungary", "hungarian" }),
            (Queries: new[] { "local", "macedonian", "macedonia", "skopje" }, Profiles: new[] { "north macedonia", "macedonia", "macedonian" })
        };

        var requested = origins.FirstOrDefault(origin =>
            origin.Queries.Any(term => ContainsTerm(normalizedQuery, term)));

        if (requested.Queries == null)
        {
            return beers;
        }

        return beers
            .Where(beer => requested.Profiles.Any(origin =>
                ContainsTerm(Normalize(beer.OriginCountry), origin)))
            .ToList();
    }

    private static IReadOnlyCollection<Product> RestrictToExplicitPairing(
        string query,
        IReadOnlyCollection<Product> beers)
    {
        var normalizedQuery = Normalize(query);
        var pairingGroups = new[]
        {
            new[] { "burger", "burgers" },
            new[] { "pizza" },
            new[] { "wing", "wings" },
            new[] { "sausage", "sausages" },
            new[] { "fries" },
            new[] { "cheese" },
            new[] { "chicken" },
            new[] { "corn dog" },
            new[] { "smoked meat" }
        };

        var requested = pairingGroups.FirstOrDefault(group =>
            group.Any(term => ContainsTerm(normalizedQuery, term)));

        if (requested == null)
        {
            return beers;
        }

        return beers
            .Where(beer => requested.Any(term =>
                ContainsTerm(Normalize(beer.PairingTags), term)))
            .ToList();
    }

    public bool HasUsefulPreference(string query)
    {
        var normalized = Normalize(query);

        if (AbvLimitPattern().IsMatch(normalized) ||
            PriceLimitPattern().IsMatch(normalized) ||
            CheapestPattern().IsMatch(normalized) ||
            MostExpensivePattern().IsMatch(normalized) ||
            HighestAbvPattern().IsMatch(normalized) ||
            LowestAbvPattern().IsMatch(normalized))
        {
            return true;
        }

        return WordPattern()
            .Matches(normalized)
            .Select(match => match.Value)
            .Any(word =>
                PreferenceWords.Contains(word) ||
                (word.EndsWith('s') && PreferenceWords.Contains(word[..^1])));
    }

    public string BuildEvidenceReason(Product beer, string query)
    {
        var style = beer.BeerStyle?.Trim();
        var flavours = beer.FlavorNotes?.Trim();
        var country = beer.OriginCountry?.Trim();

        var alcoholByVolume = BeerProfileQuality.AlcoholByVolume(beer);

        if (!string.IsNullOrWhiteSpace(style) ||
            !string.IsNullOrWhiteSpace(flavours))
        {
            var opening = !string.IsNullOrWhiteSpace(style)
                ? $"A {style}"
                : "A flavour-led pour";
            var taste = !string.IsNullOrWhiteSpace(flavours)
                ? $" tasting of {flavours}"
                : string.Empty;
            var origin = !string.IsNullOrWhiteSpace(country)
                ? $" from {country}"
                : string.Empty;
            var strength = alcoholByVolume.HasValue
                ? $" at {alcoholByVolume.Value:0.#}% ABV"
                : string.Empty;

            return $"{opening}{taste}{origin}{strength}.";
        }

        if (!string.IsNullOrWhiteSpace(beer.Description))
        {
            return Truncate(beer.Description.Trim(), 120).TrimEnd('.') + ".";
        }

        return "A solid match from what is available right now.";
    }

    private static double Score(
        Product beer,
        string query,
        IReadOnlyCollection<string> terms)
    {
        var name = Normalize(beer.Name);
        var style = Normalize(beer.BeerStyle ?? beer.Category?.Name);
        var flavours = Normalize(beer.FlavorNotes);
        var pairings = Normalize(beer.PairingTags);
        var country = Normalize(beer.OriginCountry);
        var description = Normalize(beer.Description);
        var normalizedQuery = Normalize(query);
        var score = 0d;

        foreach (var term in terms)
        {
            var expanded =
                (term == "sweet" && NegativeSweetPattern().IsMatch(normalizedQuery)) ||
                (term is "bitter" or "bitterness" && NegativeBitterPattern().IsMatch(normalizedQuery))
                    ? [term]
                    : Expand(term);

            foreach (var candidate in expanded)
            {
                var exactWeight = candidate.Equals(term, StringComparison.OrdinalIgnoreCase)
                    ? 1d
                    : 0.65d;

                if (ContainsTerm(name, candidate)) score += 22 * exactWeight;
                if (ContainsTerm(style, candidate)) score += 18 * exactWeight;
                if (ContainsTerm(flavours, candidate)) score += 16 * exactWeight;
                if (ContainsTerm(country, candidate)) score += 15 * exactWeight;
                if (ContainsTerm(pairings, candidate)) score += 11 * exactWeight;
                if (ContainsTerm(description, candidate)) score += 5 * exactWeight;
            }
        }

        score += PreferenceScaleScore(normalizedQuery, beer);
        score += AbvScore(normalizedQuery, BeerProfileQuality.AlcoholByVolume(beer));
        score += PriceScore(normalizedQuery, beer.Price);

        if (CheapestPattern().IsMatch(normalizedQuery))
        {
            score -= (double)beer.Price / 10;
        }

        return score;
    }

    private static double PreferenceScaleScore(string query, Product beer)
    {
        var score = 0d;
        var notSweet = NegativeSweetPattern().IsMatch(query);
        var notBitter = NegativeBitterPattern().IsMatch(query);

        if ((ContainsTerm(query, "bitter") || ContainsTerm(query, "hoppy")) &&
            beer.BitternessLevel.HasValue)
        {
            score += notBitter
                ? (6 - beer.BitternessLevel.Value) * 5
                : beer.BitternessLevel.Value * 5;
        }

        if (ContainsTerm(query, "sweet") && beer.SweetnessLevel.HasValue)
        {
            score += notSweet
                ? (6 - beer.SweetnessLevel.Value) * 5
                : beer.SweetnessLevel.Value * 5;
        }

        if (ContainsTerm(query, "sour") && beer.AcidityLevel.HasValue)
        {
            score += beer.AcidityLevel.Value * 5;
        }

        if (ContainsTerm(query, "light"))
        {
            if (beer.BodyLevel.HasValue) score += (6 - beer.BodyLevel.Value) * 4;
            var alcoholByVolume = BeerProfileQuality.AlcoholByVolume(beer);
            if (alcoholByVolume.HasValue) score += Math.Max(0, 8 - (double)alcoholByVolume.Value);
        }

        var strengthAbv = BeerProfileQuality.AlcoholByVolume(beer);
        if (ContainsTerm(query, "strong") && strengthAbv.HasValue)
        {
            score += (double)strengthAbv.Value * 3;
        }

        return score;
    }

    private static double AbvScore(string query, decimal? abv)
    {
        if (!abv.HasValue || PriceLimitPattern().IsMatch(query))
        {
            return 0;
        }

        var match = AbvLimitPattern().Match(query);

        if (!match.Success ||
            !decimal.TryParse(match.Groups[2].Value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var requested))
        {
            return 0;
        }

        var lowerBound = match.Groups[1].Value is "over" or "above" or "more";
        var fits = lowerBound ? abv.Value > requested : abv.Value < requested;
        return fits ? 35 : -35;
    }

    private static double PriceScore(string query, decimal price)
    {
        var match = PriceLimitPattern().Match(query);

        if (!match.Success ||
            !decimal.TryParse(match.Groups[2].Value, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var requested))
        {
            return 0;
        }

        var lowerBound = match.Groups[1].Value is "over" or "above" or "more";
        var fits = lowerBound ? price > requested : price < requested;
        return fits ? 35 : -35;
    }

    private static IReadOnlyCollection<string> QueryTerms(string query) =>
        WordPattern()
            .Matches(Normalize(query))
            .Select(match => match.Value)
            .Where(term => term.Length > 2 && !IgnoredWords.Contains(term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IEnumerable<string> Expand(string term) =>
        Aliases.TryGetValue(term, out var aliases)
            ? aliases.Prepend(term).Distinct(StringComparer.OrdinalIgnoreCase)
            : [term];

    private static bool ContainsTerm(string source, string term) =>
        Regex.IsMatch(source, $@"\b{Regex.Escape(term)}\b", RegexOptions.IgnoreCase);

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..length].TrimEnd() + "...";

    [GeneratedRegex(@"[a-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\b(not|no|without|less)\s+(too\s+)?sweet\b", RegexOptions.IgnoreCase)]
    private static partial Regex NegativeSweetPattern();

    [GeneratedRegex(@"\b(?:refreshing|refreshment|refresh|summery|(?:hot|warm)\s+(?:day|days|weather|outside|summer))\b", RegexOptions.IgnoreCase)]
    private static partial Regex RefreshingPattern();

    [GeneratedRegex(@"\b(not|no|without|less)\s+(too\s+)?bitter\b", RegexOptions.IgnoreCase)]
    private static partial Regex NegativeBitterPattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*%?", RegexOptions.IgnoreCase)]
    private static partial Regex AbvLimitPattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(?:mkd|denars?|денари?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PriceLimitPattern();

    [GeneratedRegex(@"\b(cheaper|cheapest|lowest price|budget)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CheapestPattern();

    [GeneratedRegex(@"\b(most expensive|priciest|highest price|highest-priced|costliest)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MostExpensivePattern();

    [GeneratedRegex(@"\b(?:highest|strongest|most\s+alcoholic|highest[-\s]*(?:alcohol|abv)|high\s*%?\s*abv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighestAbvPattern();

    [GeneratedRegex(@"\b(?:lowest|weakest|least\s+alcoholic|lowest[-\s]*(?:alcohol|abv)|low\s*%?\s*abv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowestAbvPattern();
}
