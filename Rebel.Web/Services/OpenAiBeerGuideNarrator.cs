using System.Text.Json;
using OpenAI.Responses;
using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public class OpenAiBeerGuideNarrator : IBeerGuideNarrator
{
    private readonly ResponsesClient? _client;
    private readonly string _model;
    private readonly ILogger<OpenAiBeerGuideNarrator> _logger;

    public OpenAiBeerGuideNarrator(
        IConfiguration configuration,
        ILogger<OpenAiBeerGuideNarrator> logger)
    {
        var apiKey = configuration["OpenAI:ApiKey"];

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new ResponsesClient(apiKey);
        }

        _model = configuration["OpenAI:Model"] ?? "gpt-5-mini";
        _logger = logger;
    }

    public async Task<BeerGuideNarrationResult> EnrichAsync(
        BeerGuideRequest request,
        Product? food,
        IReadOnlyList<BeerGuideRecommendation> recommendations,
        CancellationToken cancellationToken)
    {
        if (_client == null || recommendations.Count == 0)
        {
            return new BeerGuideNarrationResult(recommendations, false);
        }

        try
        {
            var options = new CreateResponseOptions
            {
                Model = _model,
                MaxOutputTokenCount = 300,
                ReasoningOptions = new ResponseReasoningOptions
                {
                    ReasoningEffortLevel = ResponseReasoningEffortLevel.Low
                }
            };

            options.InputItems.Add(
                ResponseItem.CreateUserMessageItem(
                    BuildPrompt(request, food, recommendations)));

            ResponseResult response = await _client.CreateResponseAsync(
                options,
                cancellationToken);

            var reasons = ParseReasons(
                response.GetOutputText(),
                recommendations);

            if (reasons.Count == 0)
            {
                return new BeerGuideNarrationResult(recommendations, false);
            }

            var enriched = recommendations
                .Select(recommendation => new BeerGuideRecommendation
                {
                    Beer = recommendation.Beer,
                    Label = recommendation.Label,
                    Score = recommendation.Score,
                    Reason = reasons.TryGetValue(
                        recommendation.Beer.Id,
                        out var reason)
                            ? reason
                            : recommendation.Reason
                })
                .ToList();

            return new BeerGuideNarrationResult(enriched, true);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException ||
            !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                exception,
                "OpenAI narration failed; built-in explanations were used.");

            return new BeerGuideNarrationResult(recommendations, false);
        }
    }

    private static string BuildPrompt(
        BeerGuideRequest request,
        Product? food,
        IReadOnlyList<BeerGuideRecommendation> recommendations)
    {
        var payload = new
        {
            guest = new
            {
                mode = request.Mode,
                taste = request.Taste,
                intensity = request.Intensity,
                adventure = request.Adventure,
                food = food?.Name
            },
            candidates = recommendations.Select(recommendation => new
            {
                id = recommendation.Beer.Id,
                role = recommendation.Label,
                name = recommendation.Beer.Name,
                category = recommendation.Beer.Category?.Name,
                style = recommendation.Beer.BeerStyle,
                country = recommendation.Beer.OriginCountry,
                abv = recommendation.Beer.AlcoholByVolume,
                description = recommendation.Beer.Description,
                flavours = recommendation.Beer.FlavorNotes,
                pairingTags = recommendation.Beer.PairingTags,
                localReason = recommendation.Reason
            })
        };

        return $$"""
            You write short beer recommendations for Rebel Rebel, an alternative pub in Skopje.
            Use only facts in the JSON below. Never invent a product, price, ingredient, origin,
            flavour, allergen, or availability claim. Keep each reason under 28 words.
            Make Best match direct, Safe pick reassuring, and Wild card playful but clear.
            Do not encourage excessive drinking and do not make health claims.

            Return JSON only in exactly this shape:
            {"reasons":[{"id":"product-guid","reason":"one short sentence"}]}
            Include each supplied candidate exactly once and keep every id unchanged.

            DATA:
            {{JsonSerializer.Serialize(payload)}}
            """;
    }

    private static Dictionary<Guid, string> ParseReasons(
        string? output,
        IReadOnlyList<BeerGuideRecommendation> recommendations)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var allowedIds = recommendations
            .Select(recommendation => recommendation.Beer.Id)
            .ToHashSet();

        var result = new Dictionary<Guid, string>();

        using var document = JsonDocument.Parse(output);

        if (!document.RootElement.TryGetProperty("reasons", out var reasons) ||
            reasons.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in reasons.EnumerateArray())
        {
            if (!item.TryGetProperty("id", out var idProperty) ||
                !Guid.TryParse(idProperty.GetString(), out var id) ||
                !allowedIds.Contains(id) ||
                !item.TryGetProperty("reason", out var reasonProperty))
            {
                continue;
            }

            var reason = reasonProperty.GetString()?.Trim();

            if (string.IsNullOrWhiteSpace(reason) || reason.Length > 220)
            {
                continue;
            }

            result[id] = reason;
        }

        return result;
    }

}
