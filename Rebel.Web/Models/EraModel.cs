namespace Rebel.Web.Models;

/// <summary>
/// One stop on the Bowie timeline that runs down the side of a chapter:
/// the year, a small icon from the layout's sprite and the song or persona.
/// </summary>
public sealed record EraModel(string Year, string Icon, string Title, string? CssClass = null);
