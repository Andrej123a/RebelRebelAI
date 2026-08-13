using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Infrastructure.Data;
using Rebel.Web.Authorization;
using Rebel.Web.Models;
using Rebel.Web.Services;

namespace Rebel.Web.Controllers;

[Authorize(Policy = AdminPolicies.ManagerOnly)]
public class AdminBeerFeedbackController : Controller
{
    private readonly AppDbContext _context;

    public AdminBeerFeedbackController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var feedback = await _context.BeerGuideFeedbacks
            .AsNoTracking()
            .Include(item => item.Product)
            .Where(item =>
                item.CreatedAtUtc >= BeerGuideFeedbackPolicy.LiveSinceUtc)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var model = new AdminBeerFeedbackViewModel
        {
            RatedAnswers = feedback
                .Select(item => item.ResponseId)
                .Distinct()
                .Count(),
            PositiveAnswers = feedback
                .Where(item => item.IsPositive)
                .Select(item => item.ResponseId)
                .Distinct()
                .Count(),
            NegativeAnswers = feedback
                .Where(item => !item.IsPositive)
                .Select(item => item.ResponseId)
                .Distinct()
                .Count(),
            Beers = feedback
                .GroupBy(item => new
                {
                    item.ProductId,
                    ProductName = item.Product?.Name ?? "Archived beer"
                })
                .Select(group =>
                {
                    var positive = group.Count(item => item.IsPositive);
                    var negative = group.Count() - positive;
                    var commonIssue = group
                        .Where(item => !item.IsPositive && item.Reason.HasValue)
                        .GroupBy(item => item.Reason!.Value)
                        .OrderByDescending(reasonGroup => reasonGroup.Count())
                        .ThenBy(reasonGroup => reasonGroup.Key)
                        .Select(reasonGroup => SplitWords(reasonGroup.Key.ToString()))
                        .FirstOrDefault();

                    return new AdminBeerFeedbackItemViewModel
                    {
                        ProductId = group.Key.ProductId,
                        ProductName = group.Key.ProductName,
                        Ratings = group.Count(),
                        PositiveRatings = positive,
                        NegativeRatings = negative,
                        PositivePercent = group.Any()
                            ? (int)Math.Round(positive * 100d / group.Count())
                            : 0,
                        MostCommonIssue = commonIssue,
                        BestContext = DescribeBestContext(
                            group.Where(item => item.IsPositive))
                    };
                })
                .OrderByDescending(item => item.Ratings)
                .ThenBy(item => item.ProductName)
                .ToList()
        };

        return View(model);
    }

    private static string SplitWords(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value,
            "([a-z])([A-Z])",
            "$1 $2");

    private static string? DescribeBestContext(
        IEnumerable<Rebel.Domain.Entities.BeerGuideFeedback> feedback)
    {
        var items = feedback.ToList();
        var style = MostCommon(items.Select(item => item.RequestedStyle));
        var flavour = MostCommon(items
            .SelectMany(item => (item.FlavourTags ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)));
        var food = MostCommon(items.Select(item => item.FoodPairing));
        var context = new[] { style, flavour, food }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();

        return context.Count == 0 ? null : string.Join(" / ", context);
    }

    private static string? MostCommon(IEnumerable<string?> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key)
            .Select(group => group.Key)
            .FirstOrDefault();
}
