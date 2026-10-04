using Rebel.Domain.Entities;
using Rebel.Domain.Enums;

namespace Rebel.Web.Models;

/// <summary>
/// One part of how Rebel Rebel works, for the home page's "Changes" stage, and the
/// Bowie character who looks after it (the pub is named after his song, so his
/// characters help out): the job, who does it and how he does it, a line about it in
/// the pub's own words, the colours the stage turns while he is on it, and what hangs
/// behind him ("earth", "mars" or nothing).
/// </summary>
public sealed record BowieLook(
    string Key,
    string Job,
    string Name,
    string Role,
    string Line,
    string Stage,
    string Ink,
    string Hot,
    string? Backdrop = null);

/// <summary>
/// A beer from the menu that one of the looks drinks, found by its name.
/// </summary>
public sealed record BowiePick(string LookKey, string Who, string NamePart)
{
    public bool Matches(string? beerName) =>
        beerName != null && beerName.Contains(NamePart, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The whole crew, in order of appearance. Each one is drawn in Views/Shared/Looks.
/// </summary>
public static class BowieCast
{
    public static IReadOnlyList<BowieLook> All { get; } =
    [
        new("tom", "Book a table", "Major Tom", "runs Ground Control",
            "Our booking desk is Ground Control. Ask for a table from 10:00 to 22:00, at least two hours ahead, and you get a code to follow your request from lift-off.",
            "rgba(14, 8, 7, 0.5)", "#fff1dc", "#9ad3f0", "earth"),
        new("mars", "The kitchen", "Life on Mars?", "is on the cover of the menu",
            "Fat Kitchen cooks for Rebel: burgers, wings, pizza and plates for a table that is still deciding what to drink.",
            "#a8321a", "#fff1dc", "#9ad3f0", "mars"),
        new("ziggy", "The fridge", "Ziggy Stardust", "plays the beers",
            "More than fifty beers, laid out like a record: every one a track with its own colour and taste. Ziggy always puts the same one on first.",
            "#e2401a", "#1a0f0d", "#ffd86a"),
        new("sane", "The walls", "Aladdin Sane", "is on the mural",
            "His lightning bolt runs across our mural, by the tables and on the hand-made Bowie head at the bar. Come and count them.",
            "#f2c23a", "#1a0f0d", "#bb150b"),
        new("rebel", "The name", "Rebel Rebel", "is the song we're named after",
            "Rebel Rebel by Fat Kitchen opened in May 2025: a small alternative pub in Skopje for people who wanted something warmer, stranger and more personal.",
            "#3b7cc0", "#fff1dc", "#ffd86a"),
        new("jack", "Opening hours", "Halloween Jack", "keeps the night watch",
            "Open every day from noon until one in the morning. Come for one drink, stay for a second, and let the night get less planned.",
            "#4a0803", "#f2c23a", "#fff1dc"),
        new("duke", "Getting here", "The Thin White Duke", "goes station to station",
            "Naum Naumovski Borce 62, in the centre of Skopje. No dress code, no ceremony.",
            "#1d1a19", "#f4efe6", "#e2401a"),
        new("pierrot", "Our nights", "Pierrot", "hosts the stage",
            "Live music, loud nights and the kind of stories that should probably stay at the pub.",
            "#5b1f45", "#f4efe6", "#9ad3f0"),
        new("prophet", "Rebel AI", "The Blind Prophet", "reads your order",
            "Can't decide? Our AI can't see a thing, but it knows the whole menu. Tell it what kind of night it is and it picks the food, the beer or both.",
            "#b9b9b9", "#1a0f0d", "#1a0f0d"),
    ];

    /// <summary>The astronaut beers, one for each spaceman.</summary>
    public static IReadOnlyList<BowiePick> Picks { get; } =
    [
        new("ziggy", "Ziggy", "Galaxy Lager"),
        new("tom", "Major Tom", "Galaxis"),
    ];

    /// <summary>Whose pick a beer is, if anyone's.</summary>
    public static BowiePick? PickFor(string? beerName) =>
        Picks.FirstOrDefault(pick => pick.Matches(beerName));

    /// <summary>A look's beer on the menu right now (available, not deleted), or null.</summary>
    public static Product? FindPick(IEnumerable<Product> menu, string lookKey)
    {
        var pick = Picks.FirstOrDefault(candidate => candidate.LookKey == lookKey);

        return pick == null
            ? null
            : menu.FirstOrDefault(product =>
                !product.IsDeleted &&
                product.IsAvailable &&
                product.Category?.Type == CategoryType.Beer &&
                pick.Matches(product.Name));
    }

    /// <summary>
    /// What Life on Mars? orders today: one of the popular dishes (or any dish, if
    /// none is marked popular), a different one each day.
    /// </summary>
    public static Product? DishOfTheDay(IEnumerable<Product> menu, DateTime skopjeToday)
    {
        var food = menu
            .Where(product =>
                !product.IsDeleted &&
                product.IsAvailable &&
                product.Category?.Type == CategoryType.Food)
            .OrderBy(product => product.Name)
            .ToList();

        var favourites = food.Where(product => product.IsPopular).ToList();
        var dishes = favourites.Count > 0 ? favourites : food;

        return dishes.Count > 0 ? dishes[skopjeToday.DayOfYear % dishes.Count] : null;
    }
}
