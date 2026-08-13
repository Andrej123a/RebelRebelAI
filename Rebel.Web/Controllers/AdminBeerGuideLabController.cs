using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Authorization;
using Rebel.Web.Models;
using Rebel.Web.Services;
using System.Text.Json;

namespace Rebel.Web.Controllers;

[Authorize(Policy = AdminPolicies.ManagerOnly)]
public sealed class AdminBeerGuideLabController : Controller
{
    private const string AiResultTempDataKey = "BeerGuideLabAiResult";
    private readonly AppDbContext _context;
    private readonly IBeerGuideTestLabService _lab;
    private readonly IBeerGuideChatService _chatService;

    public AdminBeerGuideLabController(
        AppDbContext context,
        IBeerGuideTestLabService lab,
        IBeerGuideChatService chatService)
    {
        _context = context;
        _lab = lab;
        _chatService = chatService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var beers = await LoadBeers(cancellationToken);
        AdminBeerGuideLabScenarioViewModel? aiResult = null;

        if (TempData[AiResultTempDataKey] is string serialized)
        {
            var stored = JsonSerializer.Deserialize<StoredAiResult>(serialized);
            var scenario = _lab.Scenarios.FirstOrDefault(item => item.Key == stored?.Key);

            if (stored != null && scenario != null)
            {
                var byId = beers.ToDictionary(beer => beer.Id);
                var matches = stored.ProductIds
                    .Where(byId.ContainsKey)
                    .Select(id => byId[id])
                    .ToList();

                aiResult = _lab.Evaluate(
                    scenario,
                    matches,
                    isAiTest: true,
                    usedAi: stored.UsedAi,
                    reply: stored.Reply);
            }
        }

        return View(new AdminBeerGuideLabViewModel
        {
            Scenarios = _lab.RunDeterministic(beers),
            AiResult = aiResult
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAi(
        string key,
        CancellationToken cancellationToken)
    {
        var scenario = _lab.Scenarios.FirstOrDefault(item => item.Key == key);
        if (scenario == null)
        {
            return NotFound();
        }

        var beers = await LoadBeers(cancellationToken);
        var result = await _chatService.ReplyAsync(
            scenario.Prompt,
            [],
            beers,
            new Dictionary<Guid, double>(),
            cancellationToken);

        TempData[AiResultTempDataKey] = JsonSerializer.Serialize(new StoredAiResult
        {
            Key = key,
            Reply = result.Reply,
            UsedAi = result.UsedAi,
            ProductIds = result.Matches.Select(match => match.Beer.Id).ToList()
        });

        return Redirect($"{Url.Action(nameof(Index))}#ai-result");
    }

    private Task<List<Rebel.Domain.Entities.Product>> LoadBeers(
        CancellationToken cancellationToken) =>
        _context.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product =>
                !product.IsDeleted &&
                product.Category != null &&
                product.Category.Type == CategoryType.Beer)
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

    private sealed class StoredAiResult
    {
        public string Key { get; init; } = string.Empty;

        public string Reply { get; init; } = string.Empty;

        public bool UsedAi { get; init; }

        public IReadOnlyList<Guid> ProductIds { get; init; } = [];
    }
}
