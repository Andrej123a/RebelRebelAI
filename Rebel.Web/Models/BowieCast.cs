namespace Rebel.Web.Models;

/// <summary>
/// A look on the home page's "Changes" stage: the year, who he was, the song and a
/// line about it, the colours the stage turns while he is on it, and what hangs
/// behind him (a symbol from the layout's sprite, or "earth" / "mars").
/// </summary>
public sealed record BowieLook(
    string Key,
    string Year,
    string Name,
    string Song,
    string Line,
    string Stage,
    string Ink,
    string Hot,
    string Backdrop);

/// <summary>
/// The whole cast, in order of appearance. Each one is drawn in Views/Shared/Looks.
/// </summary>
public static class BowieCast
{
    public static IReadOnlyList<BowieLook> All { get; } =
    [
        new("tom", "1969", "Major Tom", "Space Oddity",
            "An astronaut sent up in 1969 who never came back down. Our bookings desk is still his Ground Control.",
            "rgba(14, 8, 7, 0.5)", "#fff1dc", "#9ad3f0", "earth"),
        new("mars", "1971", "Life on Mars?", "Hunky Dory",
            "An ice-blue suit, blue eyeshadow and a question nobody has answered yet. Our menu is the fanzine he never printed.",
            "#a8321a", "#fff1dc", "#9ad3f0", "mars"),
        new("ziggy", "1972", "Ziggy Stardust", "Starman",
            "The starman from somewhere else, in Kansai Yamamoto's striped suit and red platform boots. He played guitar; we play the beer tracklist.",
            "#e2401a", "#1a0f0d", "#ffd86a", "sun"),
        new("sane", "1973", "Aladdin Sane", "A Lad Insane",
            "Eyes shut, a lightning bolt across his face, a tear on the collarbone. The bolt ended up on our wall, our sign and most of this website.",
            "#f2c23a", "#1a0f0d", "#bb150b", "bolt-sane"),
        new("rebel", "1974", "Rebel Rebel", "Diamond Dogs",
            "Red mullet, polka-dot scarf and an eyepatch he wore on Dutch television for an eye infection. The song this bar is named after.",
            "#3b7cc0", "#fff1dc", "#ffd86a", "record"),
        new("jack", "1974", "Halloween Jack", "Diamond Dogs",
            "A real cool cat in a yellow suit, living on the rooftops of Hunger City.",
            "#4a0803", "#f2c23a", "#fff1dc", "burst"),
        new("duke", "1976", "The Thin White Duke", "Station to Station",
            "Slicked-back hair, white shirt, black waistcoat and a cigarette. Elegant, cold and always on his way to the next station.",
            "#1d1a19", "#f4efe6", "#e2401a", "ticket"),
        new("pierrot", "1980", "Pierrot", "Ashes to Ashes",
            "The sad clown on the beach: chalk-white face, cone hat, ruff and sequins. Our events still come out on videotape.",
            "#5b1f45", "#f4efe6", "#9ad3f0", "tape"),
        new("prophet", "2016", "The Blind Prophet", "Blackstar",
            "A bandage over the eyes and buttons sewn on for new ones, from Lazarus and his last record. The last look, at the bottom of every page.",
            "#b9b9b9", "#1a0f0d", "#1a0f0d", "blackstar"),
    ];
}
