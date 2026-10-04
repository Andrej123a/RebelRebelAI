using Microsoft.AspNetCore.Mvc;

namespace Rebel.Web.Models;

/// <summary>
/// Something to do on the site, asked of the Bowie character who looks after it (see
/// BowieCast) instead of pressing a button: his painted face, what he says, and what you
/// can say back. With one reply the whole call is the link, or with Button ("submit" or
/// "button") a form's button; with more, each reply sits in his speech bubble; with none
/// he just says his line. Inline draws him without a link of his own, for a card that is
/// already one. Attributes go on the link or button as they are (hooks for scripts).
/// </summary>
public sealed record EgoCall(
    string Key,
    string Line,
    IReadOnlyList<EgoReply> Replies,
    string? CssClass = null,
    string? Button = null,
    bool Inline = false,
    IReadOnlyDictionary<string, string>? Attributes = null);

/// <summary>One thing you can say back: what you want done, and where it goes.</summary>
public sealed record EgoReply(string Act, string? Href = null, bool External = false);

/// <summary>
/// The calls the site makes again and again, in each character's own voice, so every
/// page asks the same character the same way.
/// </summary>
public static class BowieCalls
{
    public const string Directions =
        "https://www.google.com/maps/search/?api=1&query=Naum+Naumovski+Borce+62+Skopje";

    public static EgoCall Book(IUrlHelper url, string? css = null) => new(
        "tom",
        "Ground Control here. Tell me when you land and I'll hold you a table.",
        [new("Book a table", url.Action("Create", "Reservations"))],
        css);

    public static EgoCall Bookings(IUrlHelper url, string? css = null) => new(
        "tom",
        "Ground Control here. A new table, or one you've already booked?",
        [
            new("Book a table", url.Action("Create", "Reservations")),
            new("Find my booking", url.Action("Lookup", "Reservations")),
        ],
        css);

    public static EgoCall FindBooking(IUrlHelper url, string? css = null) => new(
        "tom",
        "Booked already? Read me your code and I'll find your table.",
        [new("Find my booking", url.Action("Lookup", "Reservations"))],
        css);

    public static EgoCall Food(IUrlHelper url, string? css = null) => new(
        "mars",
        "The kitchen's on: burgers, wings, pizza and plates to share.",
        [new("See the food", url.Action("Menu", "Home", new { section = "food" }))],
        css);

    public static EgoCall Beer(IUrlHelper url, string? css = null) => new(
        "ziggy",
        "More than 50 beers in the fridge. Let me play you a few.",
        [new("See the beers", url.Action("Menu", "Home", new { section = "beer" }))],
        css);

    public static EgoCall Drinks(IUrlHelper url, string? css = null) => new(
        "jack",
        "Not a beer night? The bar's open till one. Here's what's behind it.",
        [new("See the drinks", url.Action("Menu", "Home", new { section = "drink" }))],
        css);

    public static EgoCall AskAi(IUrlHelper url, string? css = null) => new(
        "prophet",
        "Tell me what you feel like. I'll see the right one for you.",
        [new("Ask Rebel AI", url.Action("Index", "BeerGuide"))],
        css);

    public static EgoCall Events(IUrlHelper url, string? css = null) => new(
        "pierrot",
        "Live music and special nights. The stage is set.",
        [new("See the events", url.Action("Index", "Events"))],
        css);

    public static EgoCall GetHere(string? css = null) => new(
        "duke",
        "Naum Naumovski Borce 62, in the centre. I'll walk you there.",
        [new("Get directions", Directions, External: true)],
        css);

    public static EgoCall Contact(IUrlHelper url, string? css = null) => new(
        "duke",
        "Station to station: the way here, the phone, the hours.",
        [new("Contact & directions", url.Action("Contact", "Home"))],
        css);

    public static EgoCall Home(IUrlHelper url, string? css = null) => new(
        "rebel",
        "Lost in space? I'll take you back to the bar.",
        [new("Back home", url.Action("Index", "Home"))],
        css);
}
