namespace Rebel.Web.Models;

public sealed record BeerMenuViewModel(
    IReadOnlyList<BeerMenuSide> Sides,
    IReadOnlyList<BeerMenuFamily> Families,
    int TotalCount,
    int AvailableCount)
{
    public IEnumerable<BeerMenuTrack> Tracks =>
        Sides.SelectMany(side => side.Tracks);
}

public sealed record BeerMenuSide(
    string Label,
    string CategoryName,
    IReadOnlyList<BeerMenuTrack> Tracks);

public sealed record BeerMenuFamily(
    string Key,
    string Label,
    string Background,
    string Ink,
    string Glow);

public sealed record BeerMenuTrack(
    Guid ProductId,
    int Number,
    string Anchor,
    string Name,
    string? Style,
    string? Origin,
    string? Description,
    decimal Price,
    decimal? AlcoholByVolume,
    string? ImageUrl,
    string CategoryName,
    bool IsAvailable,
    bool IsPopular,
    bool IsLimited,
    bool IsPromo,
    BeerMenuFamily Family,
    bool IsInverted,
    string StageBackground,
    string StageInk,
    string Accent,
    IReadOnlyList<string> FlavorNotes,
    IReadOnlyList<string> Pairings,
    IReadOnlyList<BeerTasteScale> Taste,
    string SearchText)
{
    public string NumberLabel => Number.ToString("00");

    // Anton runs about half an em per capital, so long single words need a smaller size to fit the column.
    public string NameScale
    {
        get
        {
            var longestWord = Name
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => word.Length)
                .DefaultIfEmpty(0)
                .Max();

            return longestWord > 13 || Name.Length > 28
                ? "has-xl-name"
                : longestWord > 8 || Name.Length > 18
                    ? "has-long-name"
                    : "";
        }
    }
}

public sealed record BeerTasteScale(
    string Key,
    string Label,
    string LowLabel,
    string HighLabel,
    int Percent,
    string Reading);
