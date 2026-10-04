namespace Rebel.Web.Models;

/// <summary>
/// One of the poster drawings in Views/Shared/Looks: Bowie's characters (Major Tom,
/// Life on Mars?, Ziggy, Aladdin Sane, Rebel Rebel, Halloween Jack, the Thin White
/// Duke, Pierrot and the Blind Prophet) and the delivery craft that bring the menus
/// (the hauler and the courier). Uid keeps the drawing's internal ids unique when the
/// same one is on a page twice.
/// </summary>
public sealed record LookModel(string Key, string Uid = "a", string? CssClass = null)
{
    /// <summary>The partial that draws this look, e.g. "Looks/_Tom".</summary>
    public string PartialName => "Looks/_" + char.ToUpperInvariant(Key[0]) + Key[1..];
}
