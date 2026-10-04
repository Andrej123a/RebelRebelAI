namespace Rebel.Web.Models;

/// <summary>
/// One of Bowie's characters (Major Tom, Life on Mars?, Ziggy, Aladdin Sane, Rebel Rebel,
/// Halloween Jack, the Thin White Duke, Pierrot and the Blind Prophet), painted in
/// Art/Looks and shown from its render in wwwroot/art/looks, with the CSS class that
/// places it.
/// </summary>
public sealed record LookModel(string Key, string? CssClass = null);
