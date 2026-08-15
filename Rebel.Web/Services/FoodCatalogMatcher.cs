using System.Text.RegularExpressions;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;

namespace Rebel.Web.Services;

public interface IFoodCatalogMatcher
{
    bool HasFoodPreference(string query);

    IReadOnlyList<Product> Shortlist(
        string query,
        IReadOnlyCollection<Product> foods,
        int limit);

    string BuildEvidenceReason(Product food, string query);
}

public partial class FoodCatalogMatcher : IFoodCatalogMatcher
{
    public bool HasFoodPreference(string query) =>
        FoodWordPattern().IsMatch(query) ||
        FoodSpecificTastePattern().IsMatch(RemoveWeatherHeat(query));

    public IReadOnlyList<Product> Shortlist(
        string query,
        IReadOnlyCollection<Product> foods,
        int limit)
    {
        var eligible = foods
            .Where(food =>
                !food.IsDeleted &&
                food.IsAvailable &&
                food.Category?.Type == CategoryType.Food)
            .ToList();
        var categoryRestricted = RestrictCategory(query, eligible);
        var dietaryRestricted = RestrictDietaryNeeds(query, categoryRestricted);
        var heatRestricted = RestrictHeat(query, dietaryRestricted);
        var queryTerms = Terms(query);

        return heatRestricted
            .Select(food => new
            {
                Food = food,
                Score = Score(food, query, queryTerms)
            })
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Food.IsPopular)
            .ThenBy(item => item.Food.Name)
            .Take(Math.Max(0, limit))
            .Select(item => item.Food)
            .ToList();
    }

    public string BuildEvidenceReason(Product food, string query)
    {
        var notes = string.IsNullOrWhiteSpace(food.FlavorNotes)
            ? food.Description?.Trim()
            : food.FlavorNotes.Trim();
        var profile = new List<string>();

        if (WantsHeat(query) && food.HeatLevel.HasValue)
        {
            profile.Add($"heat {food.HeatLevel}/5");
        }
        if (SaltyPattern().IsMatch(query) && food.SaltinessLevel.HasValue)
        {
            profile.Add($"saltiness {food.SaltinessLevel}/5");
        }
        if (RichPattern().IsMatch(query) && food.RichnessLevel.HasValue)
        {
            profile.Add($"richness {food.RichnessLevel}/5");
        }

        var scale = profile.Count > 0
            ? $" ({string.Join(", ", profile)})"
            : string.Empty;
        return string.IsNullOrWhiteSpace(notes)
            ? $"A strong menu match{scale}."
            : $"{notes}{scale}.";
    }

    private static IReadOnlyCollection<Product> RestrictCategory(
        string query,
        IReadOnlyCollection<Product> foods)
    {
        var category = CategoryPattern().Match(query).Groups[1].Value;
        if (string.IsNullOrWhiteSpace(category))
        {
            return foods;
        }

        var singular = category.ToLowerInvariant() switch
        {
            "burgers" => "burger",
            "pizzas" => "pizza",
            "sausages" => "sausage",
            "wings" => "wing",
            "fries" => "fries",
            _ => category.ToLowerInvariant()
        };
        return foods.Where(food =>
        {
            var catalogueText = $"{food.Name} {food.Category?.Name}";
            return catalogueText.Contains(singular, StringComparison.OrdinalIgnoreCase);
        }).ToList();
    }

    private static IReadOnlyCollection<Product> RestrictDietaryNeeds(
        string query,
        IReadOnlyCollection<Product> foods)
    {
        if (Regex.IsMatch(query, @"\bvegan\b", RegexOptions.IgnoreCase))
        {
            return foods.Where(food => food.IsVegan).ToList();
        }
        if (Regex.IsMatch(query, @"\bvegetarian\b", RegexOptions.IgnoreCase))
        {
            return foods.Where(food => food.IsVegetarian || food.IsVegan).ToList();
        }
        if (Regex.IsMatch(query, @"\bgluten[- ]?free\b", RegexOptions.IgnoreCase))
        {
            return foods.Where(food => food.IsGlutenFree).ToList();
        }

        return foods;
    }

    private static IReadOnlyCollection<Product> RestrictHeat(
        string query,
        IReadOnlyCollection<Product> foods)
    {
        if (NotHotPattern().IsMatch(query))
        {
            return foods.Where(food => food.HeatLevel is <= 2).ToList();
        }

        return foods;
    }

    private static double Score(
        Product food,
        string query,
        IReadOnlySet<string> queryTerms)
    {
        var catalogueText = Terms(string.Join(' ', new[]
        {
            food.Name,
            food.Category?.Name,
            food.Description,
            food.FlavorNotes
        }.Where(value => !string.IsNullOrWhiteSpace(value))));
        var score = queryTerms.Intersect(catalogueText).Count() * 5d;

        if (WantsHeat(query) && food.HeatLevel.HasValue)
        {
            score += food.HeatLevel.Value * 7;
        }
        if (NotHotPattern().IsMatch(query) && food.HeatLevel.HasValue)
        {
            score += (6 - food.HeatLevel.Value) * 7;
        }
        if (SaltyPattern().IsMatch(query) && food.SaltinessLevel.HasValue)
        {
            score += food.SaltinessLevel.Value * 6;
        }
        if (RichPattern().IsMatch(query) && food.RichnessLevel.HasValue)
        {
            score += food.RichnessLevel.Value * 6;
        }
        if (LightFoodPattern().IsMatch(query) && food.RichnessLevel.HasValue)
        {
            score += (6 - food.RichnessLevel.Value) * 6;
        }
        if (SweetFoodPattern().IsMatch(query) && food.SweetnessLevel.HasValue)
        {
            score += food.SweetnessLevel.Value * 5;
        }
        if (TangyPattern().IsMatch(query) && food.AcidityLevel.HasValue)
        {
            score += food.AcidityLevel.Value * 5;
        }
        if (food.IsPopular)
        {
            score += 2;
        }

        return score;
    }

    private static HashSet<string> Terms(string value) =>
        Regex.Matches(value.ToLowerInvariant(), "[a-z0-9]+")
            .Select(match => match.Value)
            .Where(term => term.Length > 2 && !IgnoredTerms.Contains(term))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> IgnoredTerms = new(
        ["and", "for", "food", "give", "have", "like", "me", "show", "some", "something", "the", "want", "with"],
        StringComparer.OrdinalIgnoreCase);

    private static bool WantsHeat(string query) =>
        HotPattern().IsMatch(RemoveWeatherHeat(query));

    private static string RemoveWeatherHeat(string query) =>
        HotWeatherPattern().Replace(query, string.Empty);

    [GeneratedRegex(@"\b(?:foods?|dishes|dish|meals?|snacks?|eat|hungry|burger|burgers|pizza|pizzas|wings?|fries|sausage|sausages|finger\s+food|vegan|vegetarian|gluten[- ]?free)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FoodWordPattern();

    [GeneratedRegex(@"\b(?:hot|spicy|salty|cheesy|crispy|crunchy|rich|indulgent)\b", RegexOptions.IgnoreCase)]
    private static partial Regex FoodSpecificTastePattern();

    [GeneratedRegex(@"\b(burgers?|pizzas?|wings?|fries|sausages?|finger\s+food)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CategoryPattern();

    [GeneratedRegex(@"\b(?:hot|spicy|fiery|chilli|chili)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HotPattern();

    [GeneratedRegex(@"\b(?:hot|warm)\s+(?:day|days|weather|outside|summer)\b", RegexOptions.IgnoreCase)]
    private static partial Regex HotWeatherPattern();

    [GeneratedRegex(@"\b(?:not(?:hing)?\s+(?:hot|spicy)|mild|no\s+heat)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotHotPattern();

    [GeneratedRegex(@"\b(?:salty|salted|savory|savoury)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SaltyPattern();

    [GeneratedRegex(@"\b(?:rich|cheesy|creamy|indulgent|heavy|filling)\b", RegexOptions.IgnoreCase)]
    private static partial Regex RichPattern();

    [GeneratedRegex(@"\b(?:light|lighter|not\s+heavy)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LightFoodPattern();

    [GeneratedRegex(@"\b(?:sweet|honey|sweet-and-salty)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SweetFoodPattern();

    [GeneratedRegex(@"\b(?:tangy|acidic|pickled|sharp)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TangyPattern();
}
