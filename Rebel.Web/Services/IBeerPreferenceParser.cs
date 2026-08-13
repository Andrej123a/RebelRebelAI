using Rebel.Domain.Enums;
using Rebel.Web.Models;

namespace Rebel.Web.Services;

public interface IBeerPreferenceParser
{
    BeerPreferenceFingerprint Parse(
        string query,
        BeerFeedbackReason? correctionReason = null);
}
