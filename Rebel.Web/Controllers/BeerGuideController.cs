using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Infrastructure.Data;
using Rebel.Web.Models;
using Rebel.Web.Services;
using System.Security.Cryptography;
using System.Text;

namespace Rebel.Web.Controllers;

[Route("RebelAI")]
public class BeerGuideController : Controller
{
    private readonly AppDbContext _context;
    private readonly IBeerRecommendationService _recommendationService;
    private readonly IBeerGuideNarrator _narrator;
    private readonly IBeerGuideChatService _chatService;
    private readonly IBeerPreferenceParser _preferenceParser;
    private readonly IBeerConversationQueryBuilder _conversationQueryBuilder;
    private readonly IBeerFeedbackLearningService _feedbackLearningService;
    private readonly IBeerChatStateService _chatStateService;

    public BeerGuideController(
        AppDbContext context,
        IBeerRecommendationService recommendationService,
        IBeerGuideNarrator narrator,
        IBeerGuideChatService chatService,
        IBeerPreferenceParser preferenceParser,
        IBeerConversationQueryBuilder conversationQueryBuilder,
        IBeerFeedbackLearningService feedbackLearningService,
        IBeerChatStateService chatStateService)
    {
        _context = context;
        _recommendationService = recommendationService;
        _narrator = narrator;
        _chatService = chatService;
        _preferenceParser = preferenceParser;
        _conversationQueryBuilder = conversationQueryBuilder;
        _feedbackLearningService = feedbackLearningService;
        _chatStateService = chatStateService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? prompt,
        CancellationToken cancellationToken)
    {
        var model = await BuildViewModel(
            new BeerGuideRequest { Intensity = 3 },
            false,
            cancellationToken);

        model.InitialChatPrompt = string.IsNullOrWhiteSpace(prompt)
            ? null
            : prompt.Trim()[..Math.Min(prompt.Trim().Length, 400)];

        return View(model);
    }

    [HttpGet("/BeerGuide")]
    public IActionResult LegacyIndex() =>
        RedirectToActionPermanent(nameof(Index));

    [HttpPost("Chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Chat(
        [FromBody] BeerChatRequest? request,
        CancellationToken cancellationToken)
    {
        var message = request?.Message?.Trim();

        if (request == null ||
            string.IsNullOrWhiteSpace(message) ||
            message.Length is < 2 or > 500)
        {
            return BadRequest(new
            {
                message = "Tell me a little more about what you are craving."
            });
        }

        var history = (request.History ?? [])
            .Where(turn => turn.Role is "user" or "assistant")
            .Where(turn => !string.IsNullOrWhiteSpace(turn.Text))
            .TakeLast(8)
            .Select(turn => new BeerChatTurn
            {
                Role = turn.Role,
                Text = turn.Text.Trim()[..Math.Min(turn.Text.Trim().Length, 500)]
            })
            .ToList();

        BeerFeedbackReason? correctionReason = null;
        if (!string.IsNullOrWhiteSpace(request.CorrectionReason))
        {
            if (!Enum.TryParse<BeerFeedbackReason>(
                    request.CorrectionReason,
                    ignoreCase: true,
                    out var parsedCorrection))
            {
                return BadRequest(new { message = "That correction was not recognized." });
            }

            correctionReason = parsedCorrection;
        }

        var effectiveMessage = ApplyCorrection(message, correctionReason);
        var stateUpdate = _chatStateService.Update(
            effectiveMessage,
            request.Preferences);
        var fullQuery = stateUpdate.EffectiveQuery;
        var preference = _preferenceParser.Parse(fullQuery, correctionReason);

        var excludedBeerIds = (request.ExcludedBeerIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(20)
            .ToList();

        var previousBeerIds = (request.PreviousBeerIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(12)
            .ToList();

        var menuProducts = await _context.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product =>
                !product.IsDeleted &&
                product.Category != null &&
                (product.Category.Type == CategoryType.Beer ||
                 product.Category.Type == CategoryType.Food))
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        var candidateMenuProducts = MenuConversationCandidateSelector.Select(
            menuProducts,
            excludedBeerIds,
            previousBeerIds,
            effectiveMessage);

        var beers = candidateMenuProducts
            .Where(product => product.Category?.Type == CategoryType.Beer)
            .ToList();

        if (!beers.Any(beer => beer.IsAvailable) && excludedBeerIds.Count > 0)
        {
            return Json(new BeerChatResponse
            {
                Reply = "I have run out of different matches for this round. Reset the chat or give me a new style direction.",
                AiWasUsed = false
            });
        }

        var beerIds = beers.Select(beer => beer.Id).ToList();
        var relevantFeedback = await _context.BeerGuideFeedbacks
            .AsNoTracking()
            .Where(feedback =>
                feedback.CreatedAtUtc >= BeerGuideFeedbackPolicy.LiveSinceUtc &&
                beerIds.Contains(feedback.ProductId))
            .ToListAsync(cancellationToken);

        var feedbackScores = _feedbackLearningService.CalculateScores(
            preference,
            relevantFeedback);

        var result = await _chatService.ReplyStructuredAsync(
            effectiveMessage,
            fullQuery,
            beers,
            feedbackScores,
            cancellationToken,
            candidateMenuProducts);

        if (result.Matches.Count > 0)
        {
            var resultKinds = result.Matches
                .Select(match => match.Beer.Category?.Type)
                .Distinct()
                .ToList();
            if (resultKinds.Count == 1)
            {
                stateUpdate.Preferences.ItemKind = resultKinds[0] == CategoryType.Food
                    ? "food"
                    : "beer";
            }
        }

        var responseId = result.Matches.Count > 0 &&
            result.Matches.All(match => match.Beer.Category?.Type == CategoryType.Beer)
            ? Guid.NewGuid()
            : (Guid?)null;

        if (responseId.HasValue)
        {
            await _context.BeerGuideResponseContexts
                .Where(context => context.CreatedAtUtc < DateTime.UtcNow.AddHours(-24))
                .ExecuteDeleteAsync(cancellationToken);

            _context.BeerGuideResponseContexts.Add(new BeerGuideResponseContext
            {
                Id = responseId.Value,
                CreatedAtUtc = DateTime.UtcNow,
                RecommendedProductIds = string.Join(
                    ',',
                    result.Matches.Select(match => match.Beer.Id)),
                RequestedStyle = preference.Style,
                FlavourTags = JoinTags(preference.Flavours),
                RequestedOrigin = preference.Origin,
                StrengthPreference = preference.Strength,
                BitternessPreference = preference.Bitterness,
                SweetnessPreference = preference.Sweetness,
                FoodPairing = preference.FoodPairing
            });

            await _context.SaveChangesAsync(cancellationToken);
        }

        return Json(new BeerChatResponse
        {
            ResponseId = responseId,
            Reply = result.Reply,
            AiWasUsed = result.UsedAi,
            FollowUps = result.FollowUps?.ToList() ?? [],
            Preferences = stateUpdate.Preferences,
            Beers = result.Matches.Select(match => new BeerChatBeerResponse
            {
                Id = match.Beer.Id,
                Name = match.Beer.Name,
                ItemType = match.Beer.Category?.Type == CategoryType.Food
                    ? "food"
                    : "beer",
                Category = match.Beer.Category?.Name,
                ImageUrl = match.Beer.ImageUrl,
                Style = match.Beer.BeerStyle,
                Country = match.Beer.OriginCountry,
                AlcoholByVolume = BeerProfileQuality.AlcoholByVolume(match.Beer),
                Price = match.Beer.Price,
                Reason = match.Reason,
                BitternessLevel = match.Beer.BitternessLevel,
                SweetnessLevel = match.Beer.SweetnessLevel,
                AcidityLevel = match.Beer.AcidityLevel,
                HeatLevel = match.Beer.HeatLevel,
                SaltinessLevel = match.Beer.SaltinessLevel,
                RichnessLevel = match.Beer.RichnessLevel,
                FlavorNotes = match.Beer.FlavorNotes,
                PairingTags = match.Beer.PairingTags
            }).ToList()
        });
    }

    [HttpPost("Feedback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Feedback(
        [FromBody] BeerChatFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ResponseId == Guid.Empty ||
            !request.IsPositive.HasValue ||
            !Guid.TryParse(request.SessionId, out var sessionId))
        {
            return BadRequest(new { message = "That rating could not be recorded." });
        }

        var productIds = request.ProductIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .Take(12)
            .ToList();

        if (productIds.Count == 0)
        {
            return BadRequest(new { message = "There are no beers to rate." });
        }

        BeerFeedbackReason? reason = null;

        if (!request.IsPositive.Value)
        {
            if (!Enum.TryParse<BeerFeedbackReason>(
                    request.Reason,
                    ignoreCase: true,
                    out var parsedReason))
            {
                return BadRequest(new { message = "Choose what missed the mark." });
            }

            reason = parsedReason;
        }

        var validProductIds = await _context.Products
            .AsNoTracking()
            .Where(product =>
                productIds.Contains(product.Id) &&
                product.Category != null &&
                product.Category.Type == CategoryType.Beer)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);

        if (validProductIds.Count != productIds.Count)
        {
            return BadRequest(new { message = "One of those beers is no longer valid." });
        }

        var sessionHash = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(sessionId.ToString("N"))));

        var alreadyRecorded = await _context.BeerGuideFeedbacks
            .AsNoTracking()
            .AnyAsync(feedback =>
                feedback.ResponseId == request.ResponseId &&
                feedback.AnonymousSessionHash == sessionHash,
                cancellationToken);

        if (alreadyRecorded)
        {
            return Ok(new { recorded = true });
        }

        var responseContext = await _context.BeerGuideResponseContexts
            .FirstOrDefaultAsync(
                context => context.Id == request.ResponseId,
                cancellationToken);

        if (responseContext == null ||
            responseContext.CreatedAtUtc < DateTime.UtcNow.AddHours(-24))
        {
            return BadRequest(new { message = "That recommendation has expired." });
        }

        var expectedProductIds = responseContext.RecommendedProductIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToHashSet();

        if (!expectedProductIds.SetEquals(productIds))
        {
            return BadRequest(new { message = "Those beers do not match the rated answer." });
        }

        var createdAtUtc = DateTime.UtcNow;

        _context.BeerGuideFeedbacks.AddRange(validProductIds.Select(productId =>
            new BeerGuideFeedback
            {
                Id = Guid.NewGuid(),
                ResponseId = request.ResponseId,
                AnonymousSessionHash = sessionHash,
                ProductId = productId,
                IsPositive = request.IsPositive.Value,
                Reason = reason,
                RequestedStyle = responseContext.RequestedStyle,
                FlavourTags = responseContext.FlavourTags,
                RequestedOrigin = responseContext.RequestedOrigin,
                StrengthPreference = responseContext.StrengthPreference,
                BitternessPreference = responseContext.BitternessPreference,
                SweetnessPreference = responseContext.SweetnessPreference,
                FoodPairing = responseContext.FoodPairing,
                CreatedAtUtc = createdAtUtc
            }));

        _context.BeerGuideResponseContexts.Remove(responseContext);

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new { recorded = true });
    }

    private static string ApplyCorrection(
        string message,
        BeerFeedbackReason? correctionReason) =>
        correctionReason switch
        {
            BeerFeedbackReason.TooBitter => $"{message} Hard constraint: low bitterness, not bitter.",
            BeerFeedbackReason.TooSweet => $"{message} Hard constraint: low sweetness, not sweet.",
            BeerFeedbackReason.TooStrong => $"{message} Hard constraint: under 5% ABV.",
            BeerFeedbackReason.TooWeak => $"{message} Hard constraint: over 7% ABV.",
            _ => message
        };

    private static string? JoinTags(IReadOnlyList<string> tags) =>
        tags.Count == 0 ? null : string.Join(',', tags);

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(
        BeerGuideRequest request,
        CancellationToken cancellationToken)
    {
        request.Mode = request.Mode is "food" ? "food" : "taste";
        request.Adventure = request.Adventure is "familiar" or "wild"
            ? request.Adventure
            : "curious";

        if (request.Mode == "taste" && string.IsNullOrWhiteSpace(request.Taste))
        {
            ModelState.AddModelError(nameof(request.Taste), "Pick the direction you feel like drinking.");
        }

        if (request.Mode == "food" && !request.FoodProductId.HasValue)
        {
            ModelState.AddModelError(nameof(request.FoodProductId), "Choose the food you want to pair.");
        }

        var model = await BuildViewModel(
            request,
            ModelState.IsValid,
            cancellationToken);

        return View(model);
    }

    private async Task<BeerGuideViewModel> BuildViewModel(
        BeerGuideRequest request,
        bool shouldRecommend,
        CancellationToken cancellationToken)
    {
        var products = await _context.Products
            .AsNoTracking()
            .Include(product => product.Category)
            .Where(product =>
                product.IsAvailable &&
                product.Category != null &&
                (product.Category.Type == CategoryType.Beer ||
                 product.Category.Type == CategoryType.Food))
            .OrderBy(product => product.Name)
            .ToListAsync(cancellationToken);

        var beers = products
            .Where(product => product.Category?.Type == CategoryType.Beer)
            .ToList();

        var foods = products
            .Where(product => product.Category?.Type == CategoryType.Food)
            .ToList();

        Product? selectedFood = null;

        if (request.Mode == "food" && request.FoodProductId.HasValue)
        {
            selectedFood = foods.FirstOrDefault(
                product => product.Id == request.FoodProductId.Value);

            if (selectedFood == null)
            {
                ModelState.AddModelError(
                    nameof(request.FoodProductId),
                    "That food is no longer available. Pick another one.");

                shouldRecommend = false;
            }
        }

        var recommendations = shouldRecommend
            ? _recommendationService.Recommend(beers, selectedFood, request).ToList()
            : [];

        var narration = shouldRecommend
            ? await _narrator.EnrichAsync(
                request,
                selectedFood,
                recommendations,
                cancellationToken)
            : new BeerGuideNarrationResult(recommendations, false);

        return new BeerGuideViewModel
        {
            Request = request,
            Foods = foods,
            AvailableBeerCount = beers.Count,
            AvailableFoodCount = foods.Count,
            HasSearched = shouldRecommend,
            AiIsConfigured = true,
            AiWasUsed = narration.UsedAi,
            Recommendations = narration.Recommendations.ToList()
        };
    }
}
