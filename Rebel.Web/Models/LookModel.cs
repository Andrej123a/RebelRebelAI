namespace Rebel.Web.Models;

/// <summary>
/// One of Bowie's looks, drawn as a printed poster (Views/Shared/Looks): Major Tom,
/// Life on Mars?, Ziggy, Aladdin Sane, Rebel Rebel, Halloween Jack, the Thin White
/// Duke, Pierrot and the Blind Prophet. Uid keeps the drawing's internal ids unique
/// when the same look is on a page twice.
/// </summary>
public sealed record LookModel(string Key, string Uid = "a", string? CssClass = null)
{
    /// <summary>The partial that draws this look, e.g. "Looks/_Tom".</summary>
    public string PartialName => "Looks/_" + char.ToUpperInvariant(Key[0]) + Key[1..];
}
