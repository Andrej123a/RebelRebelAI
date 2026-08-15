using System.Text.RegularExpressions;

namespace Rebel.Web.Services;

public static partial class BeerChatContextPolicy
{
    public static bool RefersToPreviousResults(string message) =>
        ComparePattern().IsMatch(message) ||
        ReferencePattern().IsMatch(message) ||
        WhichPattern().IsMatch(message);

    public static bool RequestsAlternatives(string message) =>
        AlternativesPattern().IsMatch(message);

    [GeneratedRegex(@"\b(compare|versus|vs\.?|between)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComparePattern();

    [GeneratedRegex(@"\b(these|those|them|both|first two|last two|the first|the second|of the two|former|latter|previous beers?|ones? shown)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"\bwhich\s+(?:one|of\s+these|of\s+those|is|was|has)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WhichPattern();

    [GeneratedRegex(@"\b(?:other|different|more|another)\s+(?:choices?|options?|beers?|ones?|picks?)\b|\b(?:anything|something)\s+else\b|\bshow\s+me\s+(?:the\s+)?others?\b", RegexOptions.IgnoreCase)]
    private static partial Regex AlternativesPattern();
}
