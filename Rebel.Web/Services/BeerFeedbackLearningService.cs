using Rebel.Domain.Entities;
using Rebel.Domain.Enums;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public class BeerFeedbackLearningService : IBeerFeedbackLearningService
{
    public IReadOnlyDictionary<Guid, double> CalculateScores(
        BeerPreferenceFingerprint preference,
        IReadOnlyCollection<BeerGuideFeedback> feedback) =>
        feedback
            .GroupBy(item => item.ProductId)
            .ToDictionary(
                group => group.Key,
                group => Math.Clamp(
                    group.Sum(item => Contribution(preference, item)) /
                    Math.Sqrt(group.Count() + 4d),
                    -4d,
                    4d));

    private static double Contribution(
        BeerPreferenceFingerprint current,
        BeerGuideFeedback previous)
    {
        var similarity = 0.25d;
        if (Matches(current.Style, previous.RequestedStyle)) similarity += 1.5;
        if (Overlaps(current.Flavours, previous.FlavourTags)) similarity += 1.5;
        if (Matches(current.Origin, previous.RequestedOrigin)) similarity += 0.75;
        if (Matches(current.Strength, previous.StrengthPreference)) similarity += 0.75;
        if (Matches(current.Bitterness, previous.BitternessPreference)) similarity += 0.75;
        if (Matches(current.Sweetness, previous.SweetnessPreference)) similarity += 0.75;
        if (Matches(current.FoodPairing, previous.FoodPairing)) similarity += 1;

        if (previous.IsPositive)
        {
            return similarity;
        }

        return previous.Reason switch
        {
            BeerFeedbackReason.AlreadyTried => 0,
            BeerFeedbackReason.WrongStyle =>
                Matches(current.Style, previous.RequestedStyle) ? -2.5 : -0.1,
            BeerFeedbackReason.WrongFlavour =>
                Overlaps(current.Flavours, previous.FlavourTags) ? -2.5 : -0.1,
            BeerFeedbackReason.TooBitter => current.Bitterness switch
            {
                "low" => -2.5,
                "high" => 0,
                _ => -0.35
            },
            BeerFeedbackReason.TooSweet => current.Sweetness switch
            {
                "low" => -2.5,
                "high" => 0,
                _ => -0.35
            },
            BeerFeedbackReason.TooStrong => current.Strength switch
            {
                "low" => -2.5,
                "high" => 0,
                _ => -0.35
            },
            BeerFeedbackReason.TooWeak => current.Strength switch
            {
                "high" => -2.5,
                "low" => 0,
                _ => -0.35
            },
            _ => -similarity
        };
    }

    private static bool Matches(string? current, string? previous) =>
        !string.IsNullOrWhiteSpace(current) &&
        current.Equals(previous, StringComparison.OrdinalIgnoreCase);

    private static bool Overlaps(
        IReadOnlyList<string> current,
        string? previousTags)
    {
        if (current.Count == 0 || string.IsNullOrWhiteSpace(previousTags))
        {
            return false;
        }

        var previous = previousTags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return current.Any(previous.Contains);
    }
}
