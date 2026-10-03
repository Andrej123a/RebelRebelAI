using Rebel.Domain.Entities;
using Rebel.Domain.Enums;

namespace Rebel.Web.Models;

/// <summary>
/// One of Bowie's looks as a member of the bar's crew, for the home page's "Changes"
/// stage: the year, who he was and the song, the job he does at Rebel Rebel and a
/// line about it, the colours the stage turns while he is on it, and what hangs
/// behind him (a symbol from the layout's sprite, or "earth" / "mars").
/// </summary>
public sealed record BowieLook(
    string Key,
    string Year,
    string Name,
    string Song,
    string Job,
    string Line,
    string Stage,
    string Ink,
    string Hot,
    string Backdrop);

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
        new("tom", "1969", "Major Tom", "Space Oddity",
            "Ground Control · bookings",
            "Ground Control takes table requests from 10:00 to 22:00, at least two hours ahead. You get a code to follow yours from lift-off.",
            "rgba(14, 8, 7, 0.5)", "#fff1dc", "#9ad3f0", "earth"),
        new("mars", "1971", "Life on Mars?", "Hunky Dory",
            "Cover star of the food fanzine",
            "Fat Kitchen does the food: burgers, wings, pizza and plates for a table that is still deciding what to drink.",
            "#a8321a", "#fff1dc", "#9ad3f0", "mars"),
        new("ziggy", "1972", "Ziggy Stardust", "Starman",
            "Plays the beer tracklist",
            "Every beer in the fridge is a track on the record, with its own colour and taste profile. Ziggy knows which one to put on first.",
            "#e2401a", "#1a0f0d", "#ffd86a", "sun"),
        new("sane", "1973", "Aladdin Sane", "A Lad Insane",
            "The bolt on our wall",
            "His lightning bolt is in our mural, across the wall by the tables and on the hand-made Bowie head by the bar. Come and count them.",
            "#f2c23a", "#1a0f0d", "#bb150b", "bolt-sane"),
        new("rebel", "1974", "Rebel Rebel", "Diamond Dogs",
            "The song we're named after",
            "Rebel Rebel by Fat Kitchen opened in May 2025: a small alternative pub in Skopje for people who wanted something warmer, stranger and more personal.",
            "#3b7cc0", "#fff1dc", "#ffd86a", "record"),
        new("jack", "1974", "Halloween Jack", "Diamond Dogs",
            "Night watch of Hunger City",
            "Open every day from noon until one in the morning. Come for one drink, stay for a second, and let the night get less planned.",
            "#4a0803", "#f2c23a", "#fff1dc", "burst"),
        new("duke", "1976", "The Thin White Duke", "Station to Station",
            "Station to Station · getting here",
            "Next station: Naum Naumovski Borce 62, in the centre of Skopje. No dress code, no ceremony.",
            "#1d1a19", "#f4efe6", "#e2401a", "ticket"),
        new("pierrot", "1980", "Pierrot", "Ashes to Ashes",
            "Hosts our nights",
            "Live music, loud nights and the kind of stories that should probably stay at the pub.",
            "#5b1f45", "#f4efe6", "#9ad3f0", "tape"),
        new("prophet", "2016", "The Blind Prophet", "Blackstar",
            "Rebel AI · reads your fortune in beer",
            "He can't see a thing, but he knows what you're craving. Tell him the kind of night it is; he picks the food, the beer or both.",
            "#b9b9b9", "#1a0f0d", "#1a0f0d", "blackstar"),
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
