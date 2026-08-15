using System.Text.RegularExpressions;

namespace Rebel.Web.Services;

public static partial class BeerChatContextPolicy
{
    public static bool RefersToPreviousResults(string message) =>
        ComparePattern().IsMatch(message) ||
        ReferencePattern().IsMatch(message) ||
        WhichPattern().IsMatch(message);

    public static bool RequestsAlternatives(string message) =>
        AlternativesPattern().IsMatch(message) ||
        DelegatedChoicePattern().IsMatch(message);

    public static bool RequestsSimilarityToPrevious(string message) =>
        SimilarityPattern().IsMatch(message);

    [GeneratedRegex(@"\b(compare|versus|vs\.?|between)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ComparePattern();

    [GeneratedRegex(@"\b(these|those|them|both|first two|last two|the first|the second|of the two|former|latter|previous beers?|ones? shown)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"\bwhich\s+(?:one|of\s+these|of\s+those|is|was|has)\b", RegexOptions.IgnoreCase)]
    private static partial Regex WhichPattern();

    [GeneratedRegex(@"\b(?:other|different|more|another)\s+(?:choices?|options?|beers?|foods?|dishes?|ones?|picks?)\b|\b(?:anything|something|what)\s+else\b|\bshow\s+me\s+(?:the\s+)?others?\b|\bsomething\s+(?:cheaper|more\s+expensive|lighter|stronger|less\s+bitter|less\s+sweet)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AlternativesPattern();

    [GeneratedRegex(@"\b(?:similar\s+(?:beers?|ones?|options?)|something\s+similar|like\s+(?:it|that|this|those|them))\b", RegexOptions.IgnoreCase)]
    private static partial Regex SimilarityPattern();

    [GeneratedRegex(@"^\s*(?:it'?s\s+on\s+you|your\s+(?:call|choice)|you\s+(?:choose|decide)|surprise\s+me|dealer'?s\s+choice)[!.?]*\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DelegatedChoicePattern();
}
