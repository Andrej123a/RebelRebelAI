using Rebel.Domain.Entities;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerFeedbackLearningService
{
    IReadOnlyDictionary<Guid, double> CalculateScores(
        BeerPreferenceFingerprint preference,
        IReadOnlyCollection<BeerGuideFeedback> feedback);
}
