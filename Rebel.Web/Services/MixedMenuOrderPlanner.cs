using System.Text.RegularExpressions;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;

namespace Rebel.Web.Services;

public sealed record MixedMenuOrderPlan(
    IReadOnlyList<Product> Foods,
    IReadOnlyList<Product> Beers,
    decimal Total,
    decimal? Budget,
    decimal? CheapestPossibleTotal);

public static partial class MixedMenuOrderPlanner
{
    public static MixedMenuOrderPlan? Build(
        IReadOnlyCollection<Product> products,
        int foodCount,
        int beerCount,
        decimal? budget)
    {
        var foods = CandidateProducts(products, CategoryType.Food, 14);
        var beers = CandidateProducts(products, CategoryType.Beer, 18);
        if (foods.Count < foodCount || beers.Count < beerCount)
        {
            return null;
        }

        var foodGroups = Combinations(foods, foodCount).ToList();
        var beerGroups = Combinations(beers, beerCount).ToList();
        var cheapest = foodGroups.Min(group => group.Sum(item => item.Price)) +
            beerGroups.Min(group => group.Sum(item => item.Price));
        var options = from foodGroup in foodGroups
                      from beerGroup in beerGroups
                      let total = foodGroup.Sum(item => item.Price) +
                          beerGroup.Sum(item => item.Price)
                      where !budget.HasValue || total <= budget.Value
                      let score = Score(foodGroup, beerGroup, total, budget)
                      orderby score descending, total descending
                      select new MixedMenuOrderPlan(
                          foodGroup,
                          beerGroup,
                          total,
                          budget,
                          cheapest);

        return options.FirstOrDefault() ?? new MixedMenuOrderPlan(
            [],
            [],
            0,
            budget,
            cheapest);
    }

    private static List<Product> CandidateProducts(
        IEnumerable<Product> products,
        CategoryType type,
        int limit) => products
        .Where(product =>
            product.IsAvailable &&
            !product.IsDeleted &&
            product.Category?.Type == type)
        .OrderByDescending(product => product.IsPopular)
        .ThenBy(product => product.Price)
        .ThenBy(product => product.Name)
        .Take(limit)
        .ToList();

    private static double Score(
        IReadOnlyList<Product> foods,
        IReadOnlyList<Product> beers,
        decimal total,
        decimal? budget)
    {
        var pairing = foods.Sum(food => beers.Sum(beer => PairingAffinity(food, beer)));
        var popularity = foods.Concat(beers).Count(product => product.IsPopular);
        var variety = beers
            .Select(beer => beer.BeerStyle?.Trim().ToLowerInvariant())
            .Where(style => !string.IsNullOrWhiteSpace(style))
            .Distinct()
            .Count();
        var utilization = budget.HasValue && budget.Value > 0
            ? (double)(total / budget.Value)
            : 0.5;
        return pairing * 60 + popularity * 12 + variety * 4 + utilization * 10;
    }

    private static int PairingAffinity(Product food, Product beer)
    {
        var foodTerms = Terms(string.Join(' ', new[]
        {
            food.Name,
            food.Category?.Name,
            food.FlavorNotes
        }));
        var beerPairings = Terms(beer.PairingTags);
        return foodTerms.Intersect(beerPairings).Count();
    }

    private static HashSet<string> Terms(string? value) => WordPattern()
        .Matches(value ?? string.Empty)
        .Select(match => match.Value.ToLowerInvariant())
        .Where(word => word.Length >= 4)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static IEnumerable<IReadOnlyList<Product>> Combinations(
        IReadOnlyList<Product> products,
        int count)
    {
        var buffer = new Product[count];
        return Walk(0, 0);

        IEnumerable<IReadOnlyList<Product>> Walk(int start, int depth)
        {
            if (depth == count)
            {
                yield return buffer.ToArray();
                yield break;
            }

            for (var index = start; index <= products.Count - (count - depth); index++)
            {
                buffer[depth] = products[index];
                foreach (var result in Walk(index + 1, depth + 1))
                {
                    yield return result;
                }
            }
        }
    }

    [GeneratedRegex(@"[a-z0-9]+", RegexOptions.IgnoreCase)]
    private static partial Regex WordPattern();
}
