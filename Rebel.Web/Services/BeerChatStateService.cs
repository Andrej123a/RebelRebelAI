using System.Globalization;
using System.Text.RegularExpressions;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public sealed partial class BeerChatStateService : IBeerChatStateService
{
    private static readonly string[] KnownStyles =
    [
        "ipa", "lager", "pilsner", "stout", "porter", "tripel",
        "sour", "wheat"
    ];

    private readonly IBeerPreferenceParser _parser;

    public BeerChatStateService(IBeerPreferenceParser parser)
    {
        _parser = parser;
    }

    public BeerChatStateUpdate Update(
        string message,
        BeerChatPreferenceState? previous)
    {
        var cleanMessage = message.Trim();
        var parsed = _parser.Parse(cleanMessage);
        var state = ShouldStartFresh(cleanMessage, parsed)
            ? new BeerChatPreferenceState()
            : Normalize(previous);
        ApplyItemKind(cleanMessage, state);
        ApplyMixedOrder(cleanMessage, state);

        foreach (var style in RejectedStyles(cleanMessage))
        {
            if (!state.ExcludedStyles.Contains(style, StringComparer.OrdinalIgnoreCase))
            {
                state.ExcludedStyles.Add(style);
            }

            if (string.Equals(state.Style, style, StringComparison.OrdinalIgnoreCase))
            {
                state.Style = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(parsed.Style))
        {
            state.Style = parsed.Style;
            state.ExcludedStyles.RemoveAll(style =>
                string.Equals(style, parsed.Style, StringComparison.OrdinalIgnoreCase));
        }

        if (parsed.Flavours.Count > 0)
        {
            state.Flavours = parsed.Flavours
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
        }

        state.Origin = parsed.Origin ?? state.Origin;
        state.Strength = parsed.Strength ?? state.Strength;
        state.Bitterness = parsed.Bitterness ?? state.Bitterness;
        state.Sweetness = parsed.Sweetness ?? state.Sweetness;
        state.FoodPairing = parsed.FoodPairing ?? state.FoodPairing;
        if (state.ItemKind == "mixed" && LightBeerPattern().IsMatch(cleanMessage))
        {
            state.Strength = "low";
        }
        ApplyFoodPreferences(cleanMessage, state);

        ApplyAbv(cleanMessage, state);
        if (state.ItemKind != "mixed")
        {
            ApplyPrice(cleanMessage, state);
        }
        if (state.ItemKind != "mixed")
        {
            ApplyCount(cleanMessage, state);
        }
        ApplySort(cleanMessage, state);

        return new BeerChatStateUpdate(state, BuildQuery(state, cleanMessage));
    }

    private static BeerChatPreferenceState Normalize(BeerChatPreferenceState? input) =>
        input == null
            ? new BeerChatPreferenceState()
            : new BeerChatPreferenceState
            {
                ItemKind = input.ItemKind is "beer" or "food" or "mixed"
                    ? input.ItemKind
                    : null,
                Style = Clean(input.Style, 30),
                Flavours = input.Flavours
                    .Select(value => Clean(value, 30))
                    .Where(value => value != null)
                    .Cast<string>()
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(6)
                    .ToList(),
                Origin = Clean(input.Origin, 30),
                Strength = Scale(input.Strength),
                Bitterness = Scale(input.Bitterness),
                Sweetness = Scale(input.Sweetness),
                Heat = Scale(input.Heat),
                Saltiness = Scale(input.Saltiness),
                Richness = Scale(input.Richness),
                FoodPairing = Clean(input.FoodPairing, 30),
                MinimumAbv = Range(input.MinimumAbv, 0, 30),
                MaximumAbv = Range(input.MaximumAbv, 0, 30),
                MinimumPrice = Range(input.MinimumPrice, 0, 10000),
                MaximumPrice = Range(input.MaximumPrice, 0, 10000),
                TargetPrice = Range(input.TargetPrice, 0, 10000),
                PriceTier = input.PriceTier is "budget" or "mid-range" or "premium"
                    ? input.PriceTier
                    : null,
                RequestedCount = input.RequestedCount is >= 1 and <= 12
                    ? input.RequestedCount
                    : null,
                RequestedBeerCount = input.RequestedBeerCount is >= 1 and <= 6
                    ? input.RequestedBeerCount
                    : null,
                RequestedFoodCount = input.RequestedFoodCount is >= 1 and <= 3
                    ? input.RequestedFoodCount
                    : null,
                TotalBudget = Range(input.TotalBudget, 50, 10000),
                Sort = input.Sort is "highest-abv" or "lowest-abv" or
                    "highest-price" or "lowest-price"
                    ? input.Sort
                    : null,
                ExcludedStyles = input.ExcludedStyles
                    .Select(value => Clean(value, 30))
                    .Where(value => value != null)
                    .Cast<string>()
                    .Where(value => KnownStyles.Contains(value, StringComparer.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(8)
                    .ToList()
            };

    private static void ApplyItemKind(string message, BeerChatPreferenceState state)
    {
        var requestedKind = RequestedItemKind(message);
        if (requestedKind == null)
        {
            return;
        }

        if (state.ItemKind != null && state.ItemKind != requestedKind)
        {
            state.Style = null;
            state.Flavours.Clear();
            state.Origin = null;
            state.Strength = null;
            state.Bitterness = null;
            state.Sweetness = null;
            state.Heat = null;
            state.Saltiness = null;
            state.Richness = null;
            state.FoodPairing = null;
            state.MinimumAbv = null;
            state.MaximumAbv = null;
            state.ExcludedStyles.Clear();
            if (state.Sort is "highest-abv" or "lowest-abv")
            {
                state.Sort = null;
            }
        }

        state.ItemKind = requestedKind;
        if (requestedKind != "mixed")
        {
            state.RequestedBeerCount = null;
            state.RequestedFoodCount = null;
            state.TotalBudget = null;
        }
    }

    private static void ApplyMixedOrder(
        string message,
        BeerChatPreferenceState state)
    {
        if (state.ItemKind != "mixed")
        {
            return;
        }

        state.RequestedBeerCount = QuantityBefore(
            message,
            BeerQuantityPattern(),
            state.RequestedBeerCount ?? 1,
            6);
        state.RequestedFoodCount = QuantityBefore(
            message,
            FoodQuantityPattern(),
            state.RequestedFoodCount ?? 1,
            3);

        var budgets = BudgetNumberPattern().Matches(message)
            .Select(match => decimal.TryParse(
                match.Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount)
                    ? amount
                    : 0)
            .Where(amount => amount >= 50 && amount <= 10000)
            .ToList();
        if (budgets.Count > 0)
        {
            state.TotalBudget = budgets.Max();
        }
    }

    private static int QuantityBefore(
        string message,
        Regex pattern,
        int fallback,
        int maximum)
    {
        var match = pattern.Match(message);
        if (!match.Success || !match.Groups[1].Success)
        {
            return fallback;
        }

        var words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["a"] = 1, ["an"] = 1, ["one"] = 1, ["two"] = 2,
            ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6
        };
        var raw = match.Groups[1].Value;
        var value = words.TryGetValue(raw, out var wordValue)
            ? wordValue
            : int.TryParse(raw, out var number) ? number : fallback;
        return Math.Clamp(value, 1, maximum);
    }

    private static void ApplyFoodPreferences(
        string message,
        BeerChatPreferenceState state)
    {
        if (state.ItemKind is not ("food" or "mixed"))
        {
            return;
        }

        var preferenceMessage = state.ItemKind == "mixed"
            ? FoodClauses(message)
            : message;

        if (LowHeatPattern().IsMatch(preferenceMessage)) state.Heat = "low";
        else if (HighHeatPattern().IsMatch(preferenceMessage)) state.Heat = "high";

        if (LowSaltPattern().IsMatch(preferenceMessage)) state.Saltiness = "low";
        else if (HighSaltPattern().IsMatch(preferenceMessage)) state.Saltiness = "high";

        if (LowRichnessPattern().IsMatch(preferenceMessage)) state.Richness = "low";
        else if (HighRichnessPattern().IsMatch(preferenceMessage)) state.Richness = "high";
    }

    private static string FoodClauses(string message)
    {
        var clauses = ClauseSeparatorPattern().Split(message)
            .Where(clause => ExplicitFoodKindPattern().IsMatch(clause))
            .ToList();
        return clauses.Count == 0 ? message : string.Join(' ', clauses);
    }

    private static string? RequestedItemKind(string message)
    {
        var food = ExplicitFoodKindPattern().IsMatch(message);
        var beer = ExplicitBeerKindPattern().IsMatch(message) ||
            ExplicitDrinkPattern().IsMatch(message);

        var rejectsBeer = NotBeerPattern().IsMatch(message);
        var rejectsFood = NotFoodPattern().IsMatch(message);
        if (rejectsBeer && !rejectsFood) return "food";
        if (rejectsFood && !rejectsBeer) return "beer";
        if (BeerPairingPattern().IsMatch(message)) return "beer";
        if (beer && food) return "mixed";
        if (beer && !food) return "beer";
        if (food && !beer) return "food";
        return null;
    }

    private static bool ShouldStartFresh(
        string message,
        BeerPreferenceFingerprint parsed) =>
        NewDirectionPattern().IsMatch(message) ||
        ObjectivePattern().IsMatch(message) ||
        NamedProfileQuestionPattern().IsMatch(message) ||
        (StandalonePreferencePattern().IsMatch(message) &&
         (!string.IsNullOrWhiteSpace(parsed.Style) ||
          !string.IsNullOrWhiteSpace(parsed.Origin) ||
          parsed.Flavours.Count > 0));

    private static IEnumerable<string> RejectedStyles(string message)
    {
        foreach (var style in KnownStyles)
        {
            if (Regex.IsMatch(
                    message,
                    $@"\b(?:no|not|without)\s+(?:a\s+)?{Regex.Escape(style)}s?\b",
                    RegexOptions.IgnoreCase))
            {
                yield return style;
            }
        }
    }

    private static void ApplyAbv(string message, BeerChatPreferenceState state)
    {
        var match = AbvPattern().Match(message);
        if (!match.Success || !decimal.TryParse(
                match.Groups[2].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value)) return;

        if (match.Groups[1].Value is "over" or "above" or "more")
        {
            state.MinimumAbv = value;
            state.MaximumAbv = null;
        }
        else
        {
            state.MaximumAbv = value;
            state.MinimumAbv = null;
        }
    }

    private static void ApplyPrice(string message, BeerChatPreferenceState state)
    {
        var intent = MenuPriceIntentParser.Parse(message);
        if (!intent.HasPreference) return;

        if (ComparativePricePattern().IsMatch(message))
        {
            state.MinimumPrice = null;
            state.MaximumPrice = null;
            state.TargetPrice = null;
            state.PriceTier = null;
        }

        if (intent.Target.HasValue)
        {
            state.TargetPrice = intent.Target;
            state.MinimumPrice = null;
            state.MaximumPrice = null;
            state.PriceTier = null;
        }
        else if (intent.Minimum.HasValue || intent.Maximum.HasValue)
        {
            state.MinimumPrice = intent.Minimum;
            state.MaximumPrice = intent.Maximum;
            state.TargetPrice = null;
            state.PriceTier = null;
        }
        else if (intent.Tier != null)
        {
            state.PriceTier = intent.Tier;
            state.MinimumPrice = null;
            state.MaximumPrice = null;
            state.TargetPrice = null;
        }
    }

    private static void ApplyCount(string message, BeerChatPreferenceState state)
    {
        var match = CountPattern().Match(message);
        if (!match.Success) return;

        var words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4,
            ["five"] = 5, ["six"] = 6
        };
        var value = words.TryGetValue(match.Value, out var wordValue)
            ? wordValue
            : int.TryParse(match.Value, out var number) ? number : 0;
        state.RequestedCount = value is >= 1 and <= 12 ? value : state.RequestedCount;
    }

    private static void ApplySort(string message, BeerChatPreferenceState state)
    {
        if (WhichChoicePattern().IsMatch(message))
        {
            state.RequestedCount = 1;
        }

        if (HighestAbvPattern().IsMatch(message)) state.Sort = "highest-abv";
        else if (LowestAbvPattern().IsMatch(message)) state.Sort = "lowest-abv";
        else if (HighestPricePattern().IsMatch(message)) state.Sort = "highest-price";
        else if (LowestPricePattern().IsMatch(message)) state.Sort = "lowest-price";
    }

    private static string BuildQuery(BeerChatPreferenceState state, string message)
    {
        var terms = new List<string>();
        if (state.ItemKind == "mixed")
        {
            terms.Add("mixed order");
            terms.Add($"{state.RequestedFoodCount ?? 1} food");
            terms.Add($"{state.RequestedBeerCount ?? 1} beers");
            if (state.TotalBudget.HasValue)
            {
                terms.Add($"total budget {state.TotalBudget:0} MKD");
            }
        }
        if (state.RequestedCount.HasValue) terms.Add($"show me {state.RequestedCount}");
        if (state.ItemKind is "beer" or "food") terms.Add(state.ItemKind);
        if (!string.IsNullOrWhiteSpace(state.Style)) terms.Add(state.Style);
        terms.AddRange(state.Flavours);
        if (!string.IsNullOrWhiteSpace(state.Origin)) terms.Add(state.Origin);
        if (state.Strength == "high") terms.Add("strong");
        if (state.Strength == "low") terms.Add("light alcohol");
        if (state.Bitterness == "high") terms.Add("bitter");
        if (state.Bitterness == "low") terms.Add("not bitter");
        if (state.Sweetness == "high") terms.Add("sweet");
        if (state.Sweetness == "low") terms.Add("not sweet");
        if (state.Heat == "high") terms.Add("spicy");
        if (state.Heat == "low") terms.Add("mild");
        if (state.Saltiness == "high") terms.Add("salty");
        if (state.Saltiness == "low") terms.Add("not salty");
        if (state.Richness == "high") terms.Add("rich");
        if (state.Richness == "low") terms.Add("light");
        if (!string.IsNullOrWhiteSpace(state.FoodPairing))
            terms.Add($"with {state.FoodPairing}");
        if (state.MinimumAbv.HasValue) terms.Add($"over {state.MinimumAbv:0.#}% ABV");
        if (state.MaximumAbv.HasValue) terms.Add($"under {state.MaximumAbv:0.#}% ABV");
        if (state.MinimumPrice.HasValue && state.MaximumPrice.HasValue)
            terms.Add($"between {state.MinimumPrice:0} and {state.MaximumPrice:0} MKD");
        else if (state.MinimumPrice.HasValue)
            terms.Add($"over {state.MinimumPrice:0} MKD");
        else if (state.MaximumPrice.HasValue)
            terms.Add($"under {state.MaximumPrice:0} MKD");
        if (state.TargetPrice.HasValue) terms.Add($"around {state.TargetPrice:0} MKD");
        if (state.PriceTier != null) terms.Add(state.PriceTier);
        terms.AddRange(state.ExcludedStyles.Select(style => $"not {style}"));
        if (state.Sort == "highest-abv") terms.Add("highest alcohol");
        if (state.Sort == "lowest-abv") terms.Add("lowest alcohol");
        if (state.Sort == "highest-price") terms.Add("most expensive");
        if (state.Sort == "lowest-price") terms.Add("cheapest");
        if (BeerChatContextPolicy.RequestsAlternatives(message)) terms.Add("other choices");

        return terms.Count == 0 ? message : string.Join(' ', terms);
    }

    private static string? Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim().ToLowerInvariant();
        return clean.Length <= maxLength ? clean : clean[..maxLength];
    }

    private static string? Scale(string? value) =>
        value is "low" or "medium" or "high" ? value : null;

    private static decimal? Range(decimal? value, decimal min, decimal max) =>
        value.HasValue && value.Value >= min && value.Value <= max ? value : null;

    [GeneratedRegex(@"^\s*(?:now|instead|actually|next|how\s+about|what\s+about|give\s+me\s+another|something\s+else)[\s,:-]+", RegexOptions.IgnoreCase)]
    private static partial Regex NewDirectionPattern();

    [GeneratedRegex(@"\b(?:highest|lowest|strongest|weakest|most\s+alcoholic|least\s+alcoholic|most\s+expensive|cheapest)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectivePattern();

    [GeneratedRegex(@"\b(?:tell\s+me\s+(?:more\s+)?about|describe|explain(?:\s+(?:to\s+)?me)?|taste\s+profile|flavou?r\s+profile|aromas?\s+(?:of|in)|where\s+is)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NamedProfileQuestionPattern();

    [GeneratedRegex(@"^\s*(?:an?\s+|some\s+|any\s+|show\s+me\s+)?[a-z-]+(?:\s+beers?)?\s*[?!.]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex StandalonePreferencePattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(?:%|percent|ABV)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AbvPattern();

    [GeneratedRegex(@"\b(?:one|two|three|four|five|six|[1-9]|1[0-2])\b(?!\s*(?:%|percent|ABV|mkd|denars?))", RegexOptions.IgnoreCase)]
    private static partial Regex CountPattern();

    [GeneratedRegex(@"\b(?:highest|strongest|most\s+alcoholic|highest[-\s]*(?:alcohol|abv))\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighestAbvPattern();

    [GeneratedRegex(@"\b(?:lowest|weakest|least\s+alcoholic|lowest[-\s]*(?:alcohol|abv))\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowestAbvPattern();

    [GeneratedRegex(@"\b(?:most\s+expensive|more\s+expensive|priciest|highest\s+price)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighestPricePattern();

    [GeneratedRegex(@"\b(?:cheapest|cheaper|lowest\s+price|least\s+expensive|budget)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowestPricePattern();

    [GeneratedRegex(@"\b(?:cheaper|more\s+expensive)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComparativePricePattern();

    [GeneratedRegex(@"\bwhich\s+(?:one|of\s+these|of\s+those|is|was|has)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WhichChoicePattern();

    [GeneratedRegex(@"\b(?:beers?|ipas?|lagers?|pilsners?|pils|stouts?|porters?|tripels?|goses?|lambics?|weissbiers?|weizens?|witbiers?|ales?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitBeerKindPattern();

    [GeneratedRegex(@"\b(?:something|anything|what|one|a)?\s*(?:to\s+drink|drink)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitDrinkPattern();

    [GeneratedRegex(@"\b(?:food|dish|meal|snack|eat|hungry|burger|burgers|pizza|pizzas|wings?|fries|sausage|sausages|chicken|vegan|vegetarian|gluten[- ]?free)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitFoodKindPattern();

    [GeneratedRegex(@"\b(?:(a|an|one|two|three|four|five|six|[1-6])\s+)?(?:(?:light|strong|dark|crisp|refreshing|hoppy|fruity|local|imported|cold|low[- ]?alcohol)\s+){0,2}(?:beers?|ipas?|lagers?|pilsners?|stouts?|porters?|sours?|ales?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BeerQuantityPattern();

    [GeneratedRegex(@"\b(?:(a|an|one|two|three|[1-3])\s+)?(?:(?:spicy|hot|mild|salty|light|rich|vegan|vegetarian|gluten[- ]?free|filling)\s+){0,2}(?:food|dish(?:es)?|meal(?:s)?|burgers?|pizzas?|wings?|sausages?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FoodQuantityPattern();

    [GeneratedRegex(@"\b[0-9]{2,5}(?:\.[0-9]+)?\b")]
    private static partial Regex BudgetNumberPattern();

    [GeneratedRegex(@"\b(?:not|no)\s+(?:a\s+)?beers?\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotBeerPattern();

    [GeneratedRegex(@"\b(?:not|no)\s+(?:any\s+)?(?:food|dish(?:es)?|meal(?:s)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotFoodPattern();

    [GeneratedRegex(@"\s*(?:,|;|\band\b|\bbut\b)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ClauseSeparatorPattern();

    [GeneratedRegex(@"\b(?:light|easy|low[- ]?alcohol)\s+beers?\b", RegexOptions.IgnoreCase)]
    private static partial Regex LightBeerPattern();

    [GeneratedRegex(@"\b(?:beer\s+pairing|pair(?:ing)?\s+(?:with|for)|beer\b.{0,45}\b(?:with|for)|(?:need|find|give|recommend)\s+(?:me\s+)?a?\s*beer|what\s+should\s+i\s+drink\s+with|what\s+beer\s+goes\s+(?:well\s+)?with)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BeerPairingPattern();

    [GeneratedRegex(@"\b(?:less\s+(?:hot|spicy)|not\s+(?:so\s+)?(?:hot|spicy)|nothing\s+(?:hot|spicy)|mild|no\s+heat)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowHeatPattern();

    [GeneratedRegex(@"\b(?:hot|spicy|fiery|chilli|chili)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighHeatPattern();

    [GeneratedRegex(@"\b(?:less\s+salty|not\s+(?:so\s+)?salty|low\s+salt|no\s+salt)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowSaltPattern();

    [GeneratedRegex(@"\b(?:salty|salted|savory|savoury)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighSaltPattern();

    [GeneratedRegex(@"\b(?:less\s+rich|not\s+(?:so\s+)?rich|light|lighter|not\s+heavy)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowRichnessPattern();

    [GeneratedRegex(@"\b(?:rich|cheesy|creamy|indulgent|heavy|filling)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighRichnessPattern();
}
