using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public class BeerRecommendationService : IBeerRecommendationService
{
    private static readonly IReadOnlyDictionary<string, string[]> TasteTerms =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["crisp"] = ["crisp", "clean", "lager", "pilsner", "light", "citrus", "dry"],
            ["hoppy"] = ["hoppy", "hop", "ipa", "bitter", "citrus", "pine", "tropical"],
            ["malty"] = ["malt", "malty", "caramel", "amber", "bock", "toast", "bread"],
            ["fruity"] = ["fruit", "fruity", "berry", "tropical", "wheat", "belgian", "banana"],
            ["dark"] = ["dark", "stout", "porter", "coffee", "chocolate", "roast", "smoke"],
            ["sour"] = ["sour", "tart", "acidic", "lambic", "gose", "saison", "wild"]
        };

    public IReadOnlyList<BeerGuideRecommendation> Recommend(
        IReadOnlyCollection<Product> beers,
        Product? food,
        BeerGuideRequest request)
    {
        var ranked = beers
            .Where(beer => beer.IsAvailable)
            .Select(beer => new RankedBeer(
                beer,
                CalculateScore(beer, food, request)))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Beer.IsPopular)
            .ThenBy(item => item.Beer.Name)
            .ToList();

        if (ranked.Count == 0)
        {
            return [];
        }

        var selected = new List<(RankedBeer Item, string Label)>();
        AddIfPresent(selected, ranked.FirstOrDefault(), "Best match");

        var safePick = ranked
            .Where(item => selected.All(entry => entry.Item.Beer.Id != item.Beer.Id))
            .OrderByDescending(item => FamiliarityScore(item.Beer))
            .ThenByDescending(item => item.Score)
            .FirstOrDefault();

        AddIfPresent(selected, safePick, "Safe pick");

        var wildCard = ranked
            .Where(item => selected.All(entry => entry.Item.Beer.Id != item.Beer.Id))
            .OrderByDescending(item => IsAdventurous(item.Beer))
            .ThenByDescending(item => item.Score)
            .FirstOrDefault();

        AddIfPresent(selected, wildCard, "Wild card");

        foreach (var remaining in ranked)
        {
            if (selected.Count >= 3)
            {
                break;
            }

            if (selected.All(entry => entry.Item.Beer.Id != remaining.Beer.Id))
            {
                selected.Add((remaining, "Also worth a pour"));
            }
        }

        return selected
            .Select(entry => new BeerGuideRecommendation
            {
                Beer = entry.Item.Beer,
                Label = entry.Label,
                Reason = BuildReason(
                    entry.Item.Beer,
                    food,
                    request,
                    entry.Label),
                Score = entry.Item.Score
            })
            .ToList();
    }

    private static int CalculateScore(
        Product beer,
        Product? food,
        BeerGuideRequest request)
    {
        var score = 10;
        var beerTerms = TermsFor(beer);

        if (!string.IsNullOrWhiteSpace(request.Taste) &&
            TasteTerms.TryGetValue(request.Taste, out var wantedTerms))
        {
            score += Math.Min(15, wantedTerms.Count(beerTerms.Contains) * 4);
            score += TasteScaleBonus(beer, request.Taste);
        }

        if (request.Intensity.HasValue)
        {
            var body = beer.BodyLevel ?? 3;
            score += Math.Max(0, 10 - Math.Abs(body - request.Intensity.Value) * 3);
        }

        score += request.Adventure switch
        {
            "familiar" when beer.IsPopular => 8,
            "wild" when IsAdventurous(beer) => 10,
            "curious" when ProfileIsComplete(beer) => 5,
            _ => 0
        };

        if (food != null)
        {
            score += FoodPairingScore(beer, food, beerTerms);
        }

        return score;
    }

    private static int TasteScaleBonus(Product beer, string taste)
    {
        return taste.ToLowerInvariant() switch
        {
            "crisp" => Math.Max(0, 6 - (beer.BodyLevel ?? 3)),
            "hoppy" => (beer.BitternessLevel ?? 3) * 2,
            "malty" => (beer.SweetnessLevel ?? 3) + (beer.BodyLevel ?? 3),
            "fruity" => (beer.SweetnessLevel ?? 3),
            "dark" => (beer.BodyLevel ?? 3) * 2,
            "sour" => (beer.AcidityLevel ?? 3) * 2,
            _ => 0
        };
    }

    private static int FoodPairingScore(
        Product beer,
        Product food,
        HashSet<string> beerTerms)
    {
        var foodTerms = TermsFor(food);
        var score = Math.Min(18, foodTerms.Intersect(beerTerms).Count() * 6);

        if (food.IsSpicy || HasAny(foodTerms, "spicy", "chilli", "hot"))
        {
            score += HasAny(beerTerms, "crisp", "citrus", "wheat", "lager", "fruity") ? 8 : 0;
        }

        if (HasAny(foodTerms, "burger", "beef", "meat", "smoked", "fried", "rich"))
        {
            score += HasAny(beerTerms, "hoppy", "ipa", "amber", "dark", "roast") ? 8 : 0;
        }

        if (HasAny(foodTerms, "cheese", "creamy"))
        {
            score += HasAny(beerTerms, "malty", "fruit", "belgian", "wheat") ? 8 : 0;
        }

        if (HasAny(foodTerms, "dessert", "sweet", "chocolate"))
        {
            score += HasAny(beerTerms, "dark", "stout", "porter", "chocolate", "fruit") ? 8 : 0;
        }

        return score;
    }

    private static string BuildReason(
        Product beer,
        Product? food,
        BeerGuideRequest request,
        string label)
    {
        var profile = DescribeBeer(beer);

        if (food != null)
        {
            return label switch
            {
                "Safe pick" => $"{profile} is the familiar, easy-going partner for {food.Name}.",
                "Wild card" => $"{profile} takes {food.Name} in a stranger, more adventurous direction.",
                _ => $"{profile} gives {food.Name} a strong counterpoint without burying the food."
            };
        }

        var taste = string.IsNullOrWhiteSpace(request.Taste)
            ? "balanced"
            : request.Taste.ToLowerInvariant();

        return label switch
        {
            "Safe pick" => $"{profile} is the familiar fallback, kept close to your chosen intensity.",
            "Wild card" => $"{profile} takes your {taste} choice somewhere less predictable.",
            _ => $"{profile} lands closest to your {taste} preference and chosen intensity."
        };
    }

    private static string DescribeBeer(Product beer)
    {
        var style = !string.IsNullOrWhiteSpace(beer.BeerStyle)
            ? beer.BeerStyle
            : beer.Category?.Name;

        var flavour = !string.IsNullOrWhiteSpace(beer.FlavorNotes)
            ? beer.FlavorNotes.Split(',')[0].Trim()
            : ExplicitFlavourFrom(beer.Description);

        if (!string.IsNullOrWhiteSpace(style) && !string.IsNullOrWhiteSpace(flavour))
        {
            return $"This {style.ToLowerInvariant()} leans {flavour.ToLowerInvariant()} and";
        }

        if (!string.IsNullOrWhiteSpace(style))
        {
            return $"This {style.ToLowerInvariant()}";
        }

        return "This available pour";
    }

    private static string? ExplicitFlavourFrom(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var knownFlavours = new[]
        {
            "citrus", "tropical fruit", "pine", "strawberry", "lime",
            "grapes", "yuzu", "raspberry", "blueberry", "sour cherry",
            "peach", "rosemary", "coffee", "chocolate"
        };

        var matches = knownFlavours
            .Where(flavour => description.Contains(
                flavour,
                StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return matches.Count > 0
            ? string.Join(" and ", matches)
            : null;
    }

    private static int FamiliarityScore(Product beer)
    {
        var score = beer.IsPopular ? 10 : 0;
        var terms = TermsFor(beer);

        if (HasAny(terms, "lager", "pilsner", "pils"))
        {
            score += 8;
        }

        if (beer.AlcoholByVolume is > 0 and <= 6)
        {
            score += 3;
        }

        if (!IsAdventurous(beer))
        {
            score += 4;
        }

        return score;
    }

    private static HashSet<string> TermsFor(Product product)
    {
        var text = string.Join(' ', new[]
        {
            product.Name,
            product.Description,
            product.Category?.Name,
            product.BeerStyle,
            product.FlavorNotes,
            product.PairingTags,
            product.IsSpicy ? "spicy" : null,
            product.IsPopular ? "popular" : null
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

        return text
            .ToLowerInvariant()
            .Split(
                [' ', ',', '.', '/', '-', ';', ':', '(', ')'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasAny(HashSet<string> terms, params string[] expected) =>
        expected.Any(terms.Contains);

    private static bool ProfileIsComplete(Product beer) =>
        !string.IsNullOrWhiteSpace(beer.BeerStyle) &&
        !string.IsNullOrWhiteSpace(beer.FlavorNotes) &&
        beer.BodyLevel.HasValue;

    private static bool IsAdventurous(Product beer)
    {
        var terms = TermsFor(beer);

        return beer.IsLimited ||
               beer.AlcoholByVolume >= 7.5m ||
               beer.AcidityLevel >= 4 ||
               beer.BodyLevel >= 5 ||
               HasAny(terms, "sour", "wild", "lambic", "gose", "tripel", "smoked");
    }

    private static void AddIfPresent(
        ICollection<(RankedBeer Item, string Label)> target,
        RankedBeer? item,
        string label)
    {
        if (item != null)
        {
            target.Add((item, label));
        }
    }

    private sealed record RankedBeer(Product Beer, int Score);
}
