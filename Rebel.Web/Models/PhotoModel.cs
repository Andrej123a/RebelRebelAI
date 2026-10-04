namespace Rebel.Web.Models;

/// <summary>
/// A real photo in a page (Views/Shared/_Photo): the one RealPhotos keeps for a key, with
/// the CSS class that places it and the width it takes on screen.
/// </summary>
public sealed record PhotoModel(string Key, string? CssClass = null, string Sizes = "(max-width: 760px) 100vw, 60vw");
