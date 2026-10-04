namespace Rebel.Web.Models;

/// <summary>
/// The thin rail that runs down the side of a chapter: the page's number in the site
/// (the same one the #open menu shows), if it has one, and what this part of Rebel is.
/// </summary>
public sealed record RailModel(string? Mark, string Title, string? CssClass = null);
