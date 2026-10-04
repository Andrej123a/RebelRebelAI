namespace Rebel.Web.Models;

/// <summary>
/// A real photograph: one of the pub's own (Src starts with "~/") or one from Unsplash,
/// with the point to keep in frame (Focus) and how far to close in on it (Zoom). The
/// photos are the scenery Bowie's characters stand in.
/// Unsplash photos are hotlinked from images.unsplash.com, as Unsplash asks, and their
/// photographer is credited next to the photo. Sky photos were taken against black space
/// and are screened onto the night sky, so only the subject shows.
/// </summary>
public sealed record RealPhoto(
    string Src,
    string Alt,
    int Width,
    int Height,
    string Color,
    string Focus = "50% 50%",
    double Zoom = 1,
    bool Sky = false,
    string? By = null,
    string? ByUser = null)
{
    private const string Referral = "utm_source=rebel_rebel&utm_medium=referral";

    private static readonly int[] Widths = [640, 960, 1280, 1920];

    public bool FromUnsplash => ByUser != null;

    public string ProfileUrl => $"https://unsplash.com/@{ByUser}?{Referral}";

    public static string UnsplashUrl => $"https://unsplash.com/?{Referral}";

    /// <summary>The photo at a given width (Unsplash resizes it on the fly).</summary>
    public string At(int width) => FromUnsplash
        ? $"https://images.unsplash.com/{Src}&auto=format&fit=max&q=72&w={width}"
        : Src;

    public string? SrcSet => FromUnsplash
        ? string.Join(", ", Widths.Where(w => w <= Width).Select(w => $"{At(w)} {w}w"))
        : null;
}

/// <summary>
/// The photo for each part of the pub, by look key (see BowieCast): behind the character
/// on the home stage, and behind the same character on that job's page.
/// </summary>
public static class RealPhotos
{
    private static readonly Dictionary<string, RealPhoto> ByKey = new()
    {
        // Major Tom: the Earth from orbit, half in night (Artemis II, 2026).
        ["tom"] = new(
            "photo-1777047023742-1607aeb1a5bb?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MjR8fGVhcnRofGVufDB8fHx8MTc5MTEwNjU4Nnwy&ixlib=rb-4.1.0",
            "The Earth from space, half of it in night",
            3840, 2560, "#05080e", "50% 50%", Sky: true, By: "NASA", ByUser: "nasa"),

        // Life on Mars?: Mars through the Hubble Space Telescope.
        ["mars"] = new(
            "photo-1710676145420-5f54d4add1e0?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8Nnx8bWFycyUyMHBsYW5ldHxlbnwwfHx8fDE3OTExMDY1ODZ8Mg&ixlib=rb-4.1.0",
            "Mars photographed by the Hubble Space Telescope",
            6400, 8000, "#0a0503", "50% 50%", Sky: true, By: "NASA Hubble Space Telescope", ByUser: "hubblespacetelescope"),

        // Ziggy plays the beers: the pub's own cans, under the mural.
        ["ziggy"] = new(
            "~/images/rebel-six-beers.webp",
            "Beer cans lined up on the bar at Rebel Rebel, under the mural",
            1086, 1448, "#2a2422", "50% 62%"),

        // Aladdin Sane's bolt.
        ["sane"] = new(
            "photo-1504123010103-b1f3fe484a32?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8OHx8bGlnaHRuaW5nJTIwc3RyaWtlJTIwbmlnaHR8ZW58MHx8fHwxNzkxMTA2NjU5fDI&ixlib=rb-4.1.0",
            "A red lightning bolt across a night sky",
            2640, 1760, "#1e0a1c", "38% 50%", By: "Johannes Plenio", ByUser: "jplenio"),

        // Rebel Rebel, on the record player.
        ["rebel"] = new(
            "photo-1616681255209-368a2cd3e643?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8N3x8dmlueWwlMjByZWNvcmQlMjB0dXJudGFibGUlMjBkYXJrfGVufDB8fHx8MTc5MTEwNjY1OXwy&ixlib=rb-4.1.0",
            "A vinyl record spinning on a turntable in red light",
            6016, 4000, "#1c0808", "42% 50%", By: "Immo Wegmann", ByUser: "tinkerman"),

        // Halloween Jack's night watch: Skopje after dark.
        ["jack"] = new(
            "photo-1742341765998-369509fedfbe?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8OHx8c2tvcGplJTIwbmlnaHR8ZW58MHx8fHwxNzkxMTA2NjU5fDI&ixlib=rb-4.1.0",
            "Skopje lit up at night, seen from above",
            4453, 5566, "#081a1a", "50% 45%", By: "Petar Avramoski", ByUser: "ernesto_petar"),

        // The Duke, station to station: the centre of Skopje.
        ["duke"] = new(
            "photo-1642291373671-29794831ebce?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8NHx8c2tvcGplJTIwbmlnaHR8ZW58MHx8fHwxNzkxMTA2NjU5fDI&ixlib=rb-4.1.0",
            "Macedonia Square in the centre of Skopje at night, under a stormy sky",
            4215, 3651, "#1a1a2c", "50% 42%", By: "Fisnik Murtezi", ByUser: "fisnikmurtezi"),

        // Pierrot hosts the stage: a band playing to a packed room.
        ["pierrot"] = new(
            "photo-1565035010268-a3816f98589a?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8NHx8bGl2ZSUyMG11c2ljJTIwY29uY2VydCUyMGNyb3dkJTIwc3RhZ2UlMjBsaWdodHN8ZW58MHx8fHwxNzkxMTA2NzQzfDI&ixlib=rb-4.1.0",
            "A guitarist playing to a crowd in warm stage light",
            4912, 7360, "#1c0a08", "55% 32%", By: "Tijs van Leur", ByUser: "tijsvl"),

        // The Blind Prophet, from Blackstar: a total solar eclipse, a black star.
        ["prophet"] = new(
            "photo-1712808261297-0a045f64422f?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MTF8fHNvbGFyJTIwZWNsaXBzZXxlbnwwfHx8fDE3OTExMDY1ODd8Mg&ixlib=rb-4.1.0",
            "A total solar eclipse: a black disc ringed with light",
            7296, 7296, "#0a0403", "50% 50%", Sky: true, By: "Robert Anderson", ByUser: "robanderson72"),

        // The courier who brings the menu: an astronaut at work outside the station.
        ["courier"] = new(
            "photo-1447433865958-f402f562b843?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MTl8fHNwYWNlJTIwc2h1dHRsZXxlbnwwfHx8fDE3OTExMDY2NTl8Mg&ixlib=rb-4.1.0",
            "An astronaut carrying cargo bags outside a spacecraft",
            4928, 3280, "#141414", "34% 55%", Sky: true, By: "NASA", ByUser: "nasa"),

        // The station the menus come down from.
        ["station"] = new(
            "photo-1614314007212-0257d6e2f7d8?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MTV8fHNwYWNlJTIwc2h1dHRsZXxlbnwwfHx8fDE3OTExMDY2NTl8Mg&ixlib=rb-4.1.0",
            "The International Space Station in orbit above the Earth",
            4289, 3217, "#0a0505", "50% 40%", Sky: true, By: "NASA", ByUser: "nasa"),
    };

    /// <summary>The photo for a look key, or null if it has none.</summary>
    public static RealPhoto? For(string key) => ByKey.GetValueOrDefault(key);
}
