namespace Rebel.Web.Models;

/// <summary>
/// One part of how Rebel Rebel works, for the home page's "How Rebel works" stage, and the
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
/// The whole crew, in order of appearance. Each one is drawn in Views/Shared/Looks.
/// </summary>
public static class BowieCast
{
    public static IReadOnlyList<BowieLook> All { get; } =
    [
        new("tom", "Book a table", "Major Tom", "runs Ground Control",
            "Ask for a table from 10:00 to 22:00, at least two hours ahead. You get a code to follow your request.",
            "rgba(14, 8, 7, 0.5)", "#fff1dc", "#9ad3f0", "earth"),
        new("mars", "The kitchen", "Life on Mars?", "is on the cover of the menu",
            "Fat Kitchen cooks for Rebel: burgers, wings, pizza and plates to share.",
            "#a8321a", "#fff1dc", "#9ad3f0", "mars"),
        new("ziggy", "The fridge", "Ziggy Stardust", "plays the beers",
            "More than 50 beers: Hungarian, Belgian, German, Czech and local, in bottles, cans and on draft.",
            "#e2401a", "#1a0f0d", "#ffd86a"),
        new("sane", "The walls", "Aladdin Sane", "is on the mural",
            "His lightning bolt runs across our mural, by the tables and on the hand-made Bowie head at the bar.",
            "#f2c23a", "#1a0f0d", "#bb150b"),
        new("rebel", "The name", "Rebel Rebel", "is the song we're named after",
            "Bowie released Rebel Rebel in 1974. The pub took its name.",
            "#3b7cc0", "#fff1dc", "#ffd86a"),
        new("jack", "Opening hours", "Halloween Jack", "keeps the night watch",
            "Open every day from 12:00 to 01:00.",
            "#4a0803", "#f2c23a", "#fff1dc"),
        new("duke", "Getting here", "The Thin White Duke", "goes station to station",
            "Naum Naumovski Borce 62, in the centre of Skopje. No dress code.",
            "#1d1a19", "#f4efe6", "#e2401a"),
        new("pierrot", "Our nights", "Pierrot", "hosts the stage",
            "Live music and special nights at the pub.",
            "#5b1f45", "#f4efe6", "#9ad3f0"),
        new("prophet", "Rebel AI", "The Blind Prophet", "reads your order",
            "Tell Rebel AI what you feel like and it picks food, beer or a pairing from the menu.",
            "#b9b9b9", "#1a0f0d", "#1a0f0d"),
    ];
}
