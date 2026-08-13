using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Authorization;
using Rebel.Web.Models;
using Rebel.Web.Services;

namespace Rebel.Web.Controllers;

[Authorize(Policy = AdminPolicies.ManagerOnly)]
public sealed class AdminBeerCatalogController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBeerCatalogMatcher _matcher;
    private readonly IBeerProfileSuggestionService _suggestionService;

    public AdminBeerCatalogController(
        AppDbContext context,
        IBeerCatalogMatcher matcher,
        IBeerProfileSuggestionService suggestionService)
    {
        _context = context;
        _matcher = matcher;
        _suggestionService = suggestionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var beers = await _context.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product =>
                !product.IsDeleted &&
                product.Category != null &&
                product.Category.Type == CategoryType.Beer)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        var availableBeers = beers.Where(beer => beer.IsAvailable).ToList();
        var items = beers.Select(beer => BuildItem(beer, availableBeers)).ToList();

        var model = new AdminBeerCatalogViewModel
        {
            Ready = items
                .Where(item => BeerProfileQuality.IsReadyForGuide(item.Beer))
                .ToList(),
            NeedsReview = items
                .Where(item => item.ReviewNotes.Count > 0)
                .ToList(),
            MissingInformation = items
                .Where(item =>
                    item.ReviewNotes.Count == 0 &&
                    item.MissingFields.Count > 0)
                .ToList()
        };

        return View(model);
    }

    private AdminBeerCatalogItemViewModel BuildItem(
        Product beer,
        IReadOnlyCollection<Product> availableBeers)
    {
        var prompts = BuildPrompts(beer);
        var tests = beer.IsAvailable
            ? prompts.Select(prompt =>
            {
                var ranked = _matcher.Shortlist(prompt, availableBeers, 8);
                var rank = ranked
                    .Select((candidate, index) => new { candidate.Id, Rank = index + 1 })
                    .FirstOrDefault(candidate => candidate.Id == beer.Id)
                    ?.Rank;

                return new AdminBeerProfileTestViewModel
                {
                    Prompt = prompt,
                    Rank = rank
                };
            }).ToList()
            : [];

        return new AdminBeerCatalogItemViewModel
        {
            Beer = beer,
            CompletionPercent = BeerProfileQuality.CompletionPercent(beer),
            MissingFields = BeerProfileQuality.MissingFields(beer),
            ReviewNotes = BeerProfileQuality.ReviewNotes(beer),
            Suggestions = _suggestionService.Suggest(beer),
            Tests = tests
        };
    }

    private static IReadOnlyList<string> BuildPrompts(Product beer)
    {
        var style = beer.BeerStyle?.Trim();
        var flavour = FirstTag(beer.FlavorNotes);
        var pairing = FirstTag(beer.PairingTags);
        var country = beer.OriginCountry?.Trim();
        var prompts = new List<string>();

        if (!string.IsNullOrWhiteSpace(flavour) && !string.IsNullOrWhiteSpace(style))
        {
            prompts.Add($"I want a {flavour} {style}");
        }

        if (!string.IsNullOrWhiteSpace(country) && !string.IsNullOrWhiteSpace(style))
        {
            prompts.Add($"Show me a {country} {style}");
        }

        if (!string.IsNullOrWhiteSpace(pairing))
        {
            prompts.Add($"A beer to drink with {pairing}");
        }

        if (prompts.Count == 0 && !string.IsNullOrWhiteSpace(style))
        {
            prompts.Add($"Show me a {style}");
        }

        return prompts
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static string? FirstTag(string? value) =>
        value?.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
}
