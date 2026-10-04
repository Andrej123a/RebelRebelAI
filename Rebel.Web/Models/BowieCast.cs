namespace Rebel.Web.Models;

/// <summary>
/// One part of how Rebel Rebel works, for the home page's "How Rebel works" stage, and the
/// Bowie character who looks after it (the pub is named after his song, so his
/// characters help out): the job, who does it and how he does it, a line about it in
/// the pub's own words, the dark the stage turns behind its photo (RealPhotos) and the
/// accent taken from that photo.
/// </summary>
public sealed record BowieLook(
    string Key,
    string Job,
    string Name,
    string Role,
    string Line,
    string Stage,
    string Hot);

/// <summary>
/// The whole crew, in order of appearance.
/// </summary>
public static class BowieCast
{
    public static IReadOnlyList<BowieLook> All { get; } =
    [
        new("tom", "Book a table", "Major Tom", "runs Ground Control",
            "Ask for a table from 10:00 to 22:00, at least two hours ahead. You get a code to follow your request.",
            "#05080e", "#9ad3f0"),
        new("mars", "The kitchen", "Life on Mars?", "is on the cover of the menu",
            "Fat Kitchen cooks for Rebel: burgers, wings, pizza and plates to share.",
            "#0b0503", "#f08a4b"),
        new("ziggy", "The fridge", "Ziggy Stardust", "plays the beers",
            "More than 50 beers: Hungarian, Belgian, German, Czech and local, in bottles, cans and on draft.",
            "#0d0807", "#f2c23a"),
        new("sane", "The walls", "Aladdin Sane", "is on the mural",
            "His lightning bolt runs across our mural, by the tables and on the hand-made Bowie head at the bar.",
            "#0e0610", "#ff5b3a"),
        new("rebel", "The name", "Rebel Rebel", "is the song we're named after",
            "Bowie released Rebel Rebel in 1974. The pub took its name.",
            "#0e0505", "#ff6a3d"),
        new("jack", "Opening hours", "Halloween Jack", "keeps the night watch",
            "Open every day from 12:00 to 01:00.",
            "#040b0c", "#f2c23a"),
        new("duke", "Getting here", "The Thin White Duke", "goes station to station",
            "Naum Naumovski Borce 62, in the centre of Skopje. No dress code.",
            "#07070e", "#a9c4f5"),
        new("pierrot", "Our nights", "Pierrot", "hosts the stage",
            "Live music and special nights at the pub.",
            "#0e0705", "#ffb35c"),
        new("prophet", "Rebel AI", "The Blind Prophet", "reads your order",
            "Tell Rebel AI what you feel like and it picks food, beer or a pairing from the menu.",
            "#070403", "#ffcf8a"),
    ];
}
