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

        ApplyAbv(cleanMessage, state);
        ApplyPrice(cleanMessage, state);
        ApplyCount(cleanMessage, state);
        ApplySort(cleanMessage, state);

        return new BeerChatStateUpdate(state, BuildQuery(state, cleanMessage));
    }

    private static BeerChatPreferenceState Normalize(BeerChatPreferenceState? input) =>
        input == null
            ? new BeerChatPreferenceState()
            : new BeerChatPreferenceState
            {
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
                FoodPairing = Clean(input.FoodPairing, 30),
                MinimumAbv = Range(input.MinimumAbv, 0, 30),
                MaximumAbv = Range(input.MaximumAbv, 0, 30),
                MinimumPrice = Range(input.MinimumPrice, 0, 10000),
                MaximumPrice = Range(input.MaximumPrice, 0, 10000),
                RequestedCount = input.RequestedCount is >= 1 and <= 12
                    ? input.RequestedCount
                    : null,
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

    private static bool ShouldStartFresh(
        string message,
        BeerPreferenceFingerprint parsed) =>
        NewDirectionPattern().IsMatch(message) ||
        ObjectivePattern().IsMatch(message) ||
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
        var match = PricePattern().Match(message);
        if (!match.Success || !decimal.TryParse(
                match.Groups[2].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var value)) return;

        if (match.Groups[1].Value is "over" or "above" or "more")
        {
            state.MinimumPrice = value;
            state.MaximumPrice = null;
        }
        else
        {
            state.MaximumPrice = value;
            state.MinimumPrice = null;
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
        if (HighestAbvPattern().IsMatch(message)) state.Sort = "highest-abv";
        else if (LowestAbvPattern().IsMatch(message)) state.Sort = "lowest-abv";
        else if (HighestPricePattern().IsMatch(message)) state.Sort = "highest-price";
        else if (LowestPricePattern().IsMatch(message)) state.Sort = "lowest-price";
    }

    private static string BuildQuery(BeerChatPreferenceState state, string message)
    {
        var terms = new List<string>();
        if (state.RequestedCount.HasValue) terms.Add($"show me {state.RequestedCount}");
        if (!string.IsNullOrWhiteSpace(state.Style)) terms.Add(state.Style);
        terms.AddRange(state.Flavours);
        if (!string.IsNullOrWhiteSpace(state.Origin)) terms.Add(state.Origin);
        if (state.Strength == "high") terms.Add("strong");
        if (state.Strength == "low") terms.Add("light alcohol");
        if (state.Bitterness == "high") terms.Add("bitter");
        if (state.Bitterness == "low") terms.Add("not bitter");
        if (state.Sweetness == "high") terms.Add("sweet");
        if (state.Sweetness == "low") terms.Add("not sweet");
        if (!string.IsNullOrWhiteSpace(state.FoodPairing))
            terms.Add($"with {state.FoodPairing}");
        if (state.MinimumAbv.HasValue) terms.Add($"over {state.MinimumAbv:0.#}% ABV");
        if (state.MaximumAbv.HasValue) terms.Add($"under {state.MaximumAbv:0.#}% ABV");
        if (state.MinimumPrice.HasValue) terms.Add($"over {state.MinimumPrice:0} MKD");
        if (state.MaximumPrice.HasValue) terms.Add($"under {state.MaximumPrice:0} MKD");
        terms.AddRange(state.ExcludedStyles.Select(style => $"not {style}"));
        if (state.Sort == "highest-abv") terms.Add("highest alcohol");
        if (state.Sort == "lowest-abv") terms.Add("lowest alcohol");
        if (state.Sort == "highest-price") terms.Add("most expensive");
        if (state.Sort == "lowest-price") terms.Add("cheapest");

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

    [GeneratedRegex(@"^\s*(?:an?\s+|some\s+|any\s+|show\s+me\s+)?[a-z-]+(?:\s+beers?)?\s*[?!.]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex StandalonePreferencePattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(?:%|percent|ABV)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AbvPattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(?:mkd|denars?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PricePattern();

    [GeneratedRegex(@"\b(?:one|two|three|four|five|six|[1-9]|1[0-2])\b(?!\s*(?:%|percent|ABV))", RegexOptions.IgnoreCase)]
    private static partial Regex CountPattern();

    [GeneratedRegex(@"\b(?:highest|strongest|most\s+alcoholic|highest[-\s]*(?:alcohol|abv))\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighestAbvPattern();

    [GeneratedRegex(@"\b(?:lowest|weakest|least\s+alcoholic|lowest[-\s]*(?:alcohol|abv))\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowestAbvPattern();

    [GeneratedRegex(@"\b(?:most\s+expensive|priciest|highest\s+price)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HighestPricePattern();

    [GeneratedRegex(@"\b(?:cheapest|lowest\s+price|budget)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LowestPricePattern();
}
