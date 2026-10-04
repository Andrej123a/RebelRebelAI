namespace Rebel.Web.Models;

/// <summary>
/// A real photo standing in a page (Views/Shared/_Look): the one RealPhotos keeps for a
/// look key, with the CSS class that places it and the width it takes on screen.
/// </summary>
public sealed record LookModel(string Key, string? CssClass = null, string Sizes = "(max-width: 760px) 100vw, 60vw");
