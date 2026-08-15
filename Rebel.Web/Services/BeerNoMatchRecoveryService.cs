using System.Globalization;
using System.Text.RegularExpressions;
using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public sealed partial class BeerNoMatchRecoveryService : IBeerNoMatchRecoveryService
{
    private readonly IBeerCatalogMatcher _matcher;

    public BeerNoMatchRecoveryService(IBeerCatalogMatcher? matcher = null)
    {
        _matcher = matcher ?? new BeerCatalogMatcher();
    }

    public BeerNoMatchRecovery Build(
        string query,
        IReadOnlyCollection<Product> availableBeers)
    {
        var followUps = new List<BeerChatFollowUp>();
        var blockedBy = new List<string>();
        var price = PricePattern().Match(query);
        var abv = AbvPattern().Match(query);

        if (price.Success)
        {
            blockedBy.Add("budget");
            var withoutFailedBudget = PricePattern().Replace(query, string.Empty);
            var closestWithoutBudget = _matcher.Shortlist(
                withoutFailedBudget,
                availableBeers,
                availableBeers.Count);
            var cheapest = closestWithoutBudget.MinBy(beer => beer.Price);
            if (cheapest != null)
            {
                var raisedBudget = Math.Ceiling(cheapest.Price / 10m) * 10m + 10m;
                followUps.Add(FollowUp(
                    $"Raise budget to {raisedBudget:0} MKD",
                    $"Keep my other preferences, but raise the budget to under {raisedBudget:0} MKD."));
                followUps.Add(FollowUp(
                    "Show the cheapest beers",
                    "Show me the three cheapest available beers."));
            }
        }

        if (!price.Success && abv.Success)
        {
            blockedBy.Add("alcohol limit");
            var knownAbvs = availableBeers
                .Select(BeerProfileQuality.AlcoholByVolume)
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();
            if (knownAbvs.Count > 0)
            {
                var direction = abv.Groups[1].Value.ToLowerInvariant();
                if (direction is "under" or "below" or "less")
                {
                    var nextLimit = Math.Ceiling(knownAbvs.Min() * 2m) / 2m + 0.5m;
                    followUps.Add(FollowUp(
                        $"Try under {nextLimit:0.#}%",
                        $"Keep my other preferences, but change the alcohol limit to under {nextLimit:0.#}% ABV."));
                }
                else
                {
                    var nextLimit = Math.Max(0, Math.Floor(knownAbvs.Max() * 2m) / 2m - 0.5m);
                    followUps.Add(FollowUp(
                        $"Try over {nextLimit:0.#}%",
                        $"Keep my other preferences, but change the alcohol limit to over {nextLimit:0.#}% ABV."));
                }
            }
        }

        if (NotBitterPattern().IsMatch(query))
        {
            blockedBy.Add("low bitterness");
            followUps.Add(FollowUp(
                "Allow medium bitterness",
                "Keep my other preferences, but medium bitterness is okay."));
        }

        if (NotSweetPattern().IsMatch(query))
        {
            blockedBy.Add("low sweetness");
            followUps.Add(FollowUp(
                "Allow medium sweetness",
                "Keep my other preferences, but medium sweetness is okay."));
        }

        var style = StylePattern().Match(query);
        if (style.Success)
        {
            var requestedStyle = style.Value.ToLowerInvariant();
            blockedBy.Add($"{requestedStyle} style");

            if (requestedStyle is "sour" or "gose" or "lambic")
            {
                followUps.Add(FollowUp(
                    "Try bright & fruity",
                    "Show me bright, fruity available beers in any beer style."));
                followUps.Add(FollowUp(
                    "Try crisp & citrusy",
                    "Show me crisp, citrusy available beers in any beer style."));
            }
            else
            {
                followUps.Add(FollowUp(
                    "Try a different style",
                    "Keep the flavour and strength preferences, but show me any beer style that fits."));
            }
        }

        var flavour = FlavourPattern().Match(query);
        if (flavour.Success)
        {
            blockedBy.Add($"{flavour.Value.ToLowerInvariant()} flavour");
            followUps.Add(FollowUp(
                "Try a nearby flavour",
                "Keep my other preferences, but show me the closest available flavour instead."));
        }

        followUps = followUps
            .DistinctBy(item => item.Prompt, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (followUps.Count == 0)
        {
            followUps.Add(FollowUp(
                "Show available favourites",
                "Show me three popular available beers."));
            followUps.Add(FollowUp(
                "Start with flavour",
                "Show me three available beers with clear flavour notes."));
        }

        var reason = blockedBy.Count == 0
            ? "those details"
            : string.Join(", ", blockedBy.Distinct(StringComparer.OrdinalIgnoreCase).Take(3));

        var reply = style.Success &&
                    style.Value is "sour" or "gose" or "lambic"
            ? "I'm afraid we do not have a sour beer available right now. I can still take you toward something bright, fruity, or citrusy."
            : $"That's a very tight order, and nothing in the fridge hits every part of it tonight. The sticking point is {reason}. Loosen one thing below and I'll keep the rest.";

        return new BeerNoMatchRecovery(reply, followUps);
    }

    private static BeerChatFollowUp FollowUp(string label, string prompt) => new()
    {
        Label = label,
        Prompt = prompt
    };

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(?:mkd|denars?|денари?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PricePattern();

    [GeneratedRegex(@"\b(under|below|less|over|above|more)\s+(?:than\s+)?([0-9]+(?:\.[0-9]+)?)\s*(?:%|percent|ABV)?", RegexOptions.IgnoreCase)]
    private static partial Regex AbvPattern();

    [GeneratedRegex(@"\b(not|no|without|less)\s+(too\s+)?bitter\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotBitterPattern();

    [GeneratedRegex(@"\b(not|no|without|less)\s+(too\s+)?sweet\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotSweetPattern();

    [GeneratedRegex(@"\b(ipa|lager|pilsner|pils|stout|porter|tripel|sour|gose|lambic|wheat|weissbier|weizen|witbier)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StylePattern();

    [GeneratedRegex(@"\b(grapefruit|citrus|citrussy|citrusy|yuzu|lemon|lime|orange|tropical|mango|passionfruit|coffee|chocolate|caramel|berry|cherry|pine|resin)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FlavourPattern();
}
