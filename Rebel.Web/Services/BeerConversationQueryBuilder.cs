using System.Text.RegularExpressions;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public partial class BeerConversationQueryBuilder : IBeerConversationQueryBuilder
{
    private readonly IBeerPreferenceParser _parser;

    public BeerConversationQueryBuilder(IBeerPreferenceParser parser)
    {
        _parser = parser;
    }

    public string Build(
        string message,
        IReadOnlyList<BeerChatTurn> history)
    {
        var currentMessage = message.Trim();
        var current = _parser.Parse(currentMessage);

        // A bare style request is a new direction, not a refinement of hidden
        // preferences from an earlier recommendation.
        if (!string.IsNullOrWhiteSpace(current.Style) &&
            StandaloneStylePattern().IsMatch(currentMessage))
        {
            return currentMessage;
        }

        if (!string.IsNullOrWhiteSpace(current.Origin) &&
            StandaloneOriginPattern().IsMatch(currentMessage))
        {
            return currentMessage;
        }

        if (ObjectiveAbvPattern().IsMatch(currentMessage))
        {
            return currentMessage;
        }

        if (NewDirectionPattern().IsMatch(currentMessage) &&
            (!string.IsNullOrWhiteSpace(current.Style) ||
             current.Flavours.Count > 0 ||
             !string.IsNullOrWhiteSpace(current.Origin)))
        {
            return currentMessage;
        }

        var resetStyle = Regex.IsMatch(
            currentMessage,
            @"\b(any|different)\s+(beer\s+)?style\b|\b(?:no|not|without)\s+(?:a\s+)?(?:ipa|lager|pilsner|pils|stout|porter|tripel|sour|gose|lambic|wheat|weissbier|weizen|witbier)\b",
            RegexOptions.IgnoreCase);
        var resetFlavour = Regex.IsMatch(
            currentMessage,
            @"\b(nearby|closest|different|any)\s+flavou?r\b",
            RegexOptions.IgnoreCase);
        var resetBitterness = Regex.IsMatch(
            currentMessage,
            @"\bmedium\s+bitterness\b",
            RegexOptions.IgnoreCase);
        var resetSweetness = Regex.IsMatch(
            currentMessage,
            @"\bmedium\s+sweetness\b",
            RegexOptions.IgnoreCase);
        var guestTurns = history
            .Where(turn => turn.Role == "user")
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Text))
            .TakeLast(8)
            .Select(turn => turn.Text.Trim())
            .ToList();

        string? inheritedStyle = null;
        IReadOnlyList<string> inheritedFlavours = [];
        string? inheritedOrigin = null;
        string? inheritedStrength = null;
        string? inheritedBitterness = null;
        string? inheritedSweetness = null;
        string? inheritedFood = null;

        foreach (var turn in guestTurns.AsEnumerable().Reverse())
        {
            var parsed = _parser.Parse(turn);
            inheritedStyle ??= parsed.Style;
            if (inheritedFlavours.Count == 0 && parsed.Flavours.Count > 0)
            {
                inheritedFlavours = parsed.Flavours;
            }

            inheritedOrigin ??= parsed.Origin;
            inheritedStrength ??= parsed.Strength;
            inheritedBitterness ??= parsed.Bitterness;
            inheritedSweetness ??= parsed.Sweetness;
            inheritedFood ??= parsed.FoodPairing;
        }

        var inheritedAbv = FindLatestAbv(guestTurns);
        var inheritedPrice = FindLatest(guestTurns, PricePattern());
        var inheritedCount = FindLatest(guestTurns, CountPattern());
        var context = new List<string>();

        if (!resetStyle)
        {
            AddWhenMissing(context, current.Style, inheritedStyle);
        }

        if (!resetFlavour && current.Flavours.Count == 0 && inheritedFlavours.Count > 0)
        {
            context.Add(string.Join(' ', inheritedFlavours));
        }

        AddWhenMissing(context, current.Origin, inheritedOrigin);
        AddWhenMissing(context, current.Strength, StrengthWords(inheritedStrength));
        if (!resetBitterness)
        {
            AddWhenMissing(context, current.Bitterness, ScaleWords(inheritedBitterness, "bitter"));
        }

        if (!resetSweetness)
        {
            AddWhenMissing(context, current.Sweetness, ScaleWords(inheritedSweetness, "sweet"));
        }

        if (string.IsNullOrWhiteSpace(current.FoodPairing) &&
            !string.IsNullOrWhiteSpace(inheritedFood))
        {
            context.Add($"with {inheritedFood}");
        }

        if (!AbvPattern().IsMatch(currentMessage) && inheritedAbv != null)
        {
            context.Add(inheritedAbv);
        }


        if (!PricePattern().IsMatch(currentMessage) && inheritedPrice != null)
        {
            context.Add(inheritedPrice);
        }

        if (!CountPattern().IsMatch(currentMessage) && inheritedCount != null)
        {
            context.Add(inheritedCount);
        }

        return context.Count == 0
            ? currentMessage
            : $"{currentMessage.TrimEnd(' ', '.', '!', '?')}. Keep these earlier preferences: {string.Join(", ", context)}.";
    }

    private static void AddWhenMissing(
        ICollection<string> context,
        string? current,
        string? inherited)
    {
        if (string.IsNullOrWhiteSpace(current) &&
            !string.IsNullOrWhiteSpace(inherited))
        {
            context.Add(inherited);
        }
    }

    private static string? FindLatest(
        IReadOnlyList<string> messages,
        Regex pattern)
    {
        foreach (var message in messages.Reverse())
        {
            var match = pattern.Match(message);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private static string? FindLatestAbv(IReadOnlyList<string> messages)
    {
        foreach (var message in messages.Reverse())
        {
            if (PricePattern().IsMatch(message))
            {
                continue;
            }

            var match = AbvPattern().Match(message);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return null;
    }

    private static string? StrengthWords(string? value) => value switch
    {
        "high" => "strong beer",
        "low" => "light alcohol",
        _ => null
    };

    private static string? ScaleWords(string? value, string dimension) => value switch
    {
        "high" => dimension,
        "low" => $"not {dimension}",
        _ => null
    };

    [GeneratedRegex(@"\b(?:under|below|less|over|above|more)\s+(?:than\s+)?[0-9]+(?:\.[0-9]+)?\s*(?:%|percent|ABV)?", RegexOptions.IgnoreCase)]
    private static partial Regex AbvPattern();

    [GeneratedRegex(@"\b(?:under|below|less|over|above|more)\s+(?:than\s+)?[0-9]+(?:\.[0-9]+)?\s*(?:mkd|denars?|денари?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PricePattern();

    [GeneratedRegex(@"\b(?:one|two|three|four|five|six|[1-6])\b(?!\s*(?:%|percent|ABV))", RegexOptions.IgnoreCase)]
    private static partial Regex CountPattern();

    [GeneratedRegex(@"^\s*(?:an?\s+)?(?:ipa|lager|pilsner|pils|stout|porter|tripel|sour|gose|lambic|wheat|weissbier|weizen|witbier)(?:\s+beer)?\s*[?!.]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex StandaloneStylePattern();

    [GeneratedRegex(@"^\s*(?:show\s+me\s+|do\s+you\s+have\s+|i(?:'d|\s+would)?\s+like\s+|some\s+|any\s+|an?\s+)?(?:hungarian|hungary|german|germany|belgian|belgium|czech|czechia|local|macedonian)(?:\s+beers?)?\s*[?!.]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex StandaloneOriginPattern();

    [GeneratedRegex(@"\b(?:highest|strongest|most\s+alcoholic|highest[-\s]*(?:alcohol|abv)|high\s*%?\s*abv)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectiveAbvPattern();

    [GeneratedRegex(@"^\s*(?:now|instead|actually|next|how\s+about|what\s+about|give\s+me\s+another|something\s+else)[\s,:-]+", RegexOptions.IgnoreCase)]
    private static partial Regex NewDirectionPattern();
}
