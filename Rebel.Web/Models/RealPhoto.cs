namespace Rebel.Web.Models;

/// <summary>
/// A real photograph: one of the pub's own (Src starts with "~/") or one from Unsplash,
/// with the point to keep in frame (Focus) and how far to close in on it (Zoom). The
/// photos are the space Bowie's characters stand in.
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

        // Ziggy Stardust: young stars flaring up in Westerlund 2.
        ["ziggy"] = new(
            "photo-1706800696671-570820e7ff39?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MXx8bmVidWxhfGVufDB8fHx8MTc5MTExMDM5MHwy&ixlib=rb-4.1.0",
            "The star cluster Westerlund 2, young stars in clouds of gas and dust",
            8919, 6683, "#0a0608", "55% 45%", Sky: true, By: "NASA Hubble Space Telescope", ByUser: "hubblespacetelescope"),

        // Aladdin Sane: Jupiter, its storms and the Great Red Spot.
        ["sane"] = new(
            "photo-1707056790571-54d8612d6368?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8M3x8anVwaXRlciUyMHBsYW5ldHxlbnwwfHx8fDE3OTExMTAzOTB8Mg&ixlib=rb-4.1.0",
            "Jupiter, photographed by the Hubble Space Telescope",
            6100, 6100, "#08060a", "50% 50%", Sky: true, By: "NASA Hubble Space Telescope", ByUser: "hubblespacetelescope"),

        // Rebel Rebel, spinning like a record: the spiral galaxy NGC 3147.
        ["rebel"] = new(
            "photo-1707057538379-d62783e77f1d?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8M3x8c3BpcmFsJTIwZ2FsYXh5fGVufDB8fHx8MTc5MTExMDM5MHwy&ixlib=rb-4.1.0",
            "The spiral galaxy NGC 3147",
            3344, 3055, "#08070c", "50% 50%", Sky: true, By: "NASA Hubble Space Telescope", ByUser: "hubblespacetelescope"),

        // Halloween Jack's night watch: Comet Leonard crossing the sky.
        ["jack"] = new(
            "photo-1640702149643-d172d1463fe4?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MTN8fGNvbWV0JTIwbmlnaHQlMjBza3l8ZW58MHx8fHwxNzkxMTEwMzkwfDI&ixlib=rb-4.1.0",
            "Comet Leonard and its long tail among the stars",
            2730, 4096, "#04101a", "50% 55%", Sky: true, By: "Jacob Dyer", ByUser: "jacobdyer"),

        // The Thin White Duke, station to station: Saturn and its rings.
        ["duke"] = new(
            "photo-1706211306706-8f36d91c8379?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8NXx8c2F0dXJuJTIwcGxhbmV0JTIwcmluZ3N8ZW58MHx8fHwxNzkxMTEwMzkwfDI&ixlib=rb-4.1.0",
            "Saturn and its rings, photographed by the Hubble Space Telescope",
            3510, 1974, "#07070a", "50% 50%", Sky: true, By: "NASA Hubble Space Telescope", ByUser: "hubblespacetelescope"),

        // Pierrot, Ashes to Ashes: the Veil Nebula, what is left of an exploded star.
        ["pierrot"] = new(
            "photo-1708112292878-fff3740bef80?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MjB8fG5lYnVsYXxlbnwwfHx8fDE3OTExMTAzOTB8Mg&ixlib=rb-4.1.0",
            "The Veil Nebula, the glowing remains of an exploded star",
            10681, 7121, "#0a0610", "50% 50%", Sky: true, By: "NASA Hubble Space Telescope", ByUser: "hubblespacetelescope"),

        // The Blind Prophet, from Blackstar: a total solar eclipse, a black star.
        ["prophet"] = new(
            "photo-1712808261297-0a045f64422f?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MTF8fHNvbGFyJTIwZWNsaXBzZXxlbnwwfHx8fDE3OTExMDY1ODd8Mg&ixlib=rb-4.1.0",
            "A total solar eclipse: a black disc ringed with light",
            7296, 7296, "#0a0403", "50% 50%", Sky: true, By: "Robert Anderson", ByUser: "robanderson72"),

        // The station the menus come down from.
        ["station"] = new(
            "photo-1614314007212-0257d6e2f7d8?ixid=M3wxMDE3MjIwfDB8MXxzZWFyY2h8MTV8fHNwYWNlJTIwc2h1dHRsZXxlbnwwfHx8fDE3OTExMDY2NTl8Mg&ixlib=rb-4.1.0",
            "The International Space Station in orbit above the Earth",
            4289, 3217, "#0a0505", "50% 40%", Sky: true, By: "NASA", ByUser: "nasa"),
    };

    /// <summary>The photo for a look key, or null if it has none.</summary>
    public static RealPhoto? For(string key) => ByKey.GetValueOrDefault(key);
}
