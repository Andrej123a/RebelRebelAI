namespace Rebel.Web.Models;

/// <summary>
/// A spinning circular badge: text running round a lightning bolt.
/// </summary>
public sealed record RebelBadgeModel(string Id, string Text, string? CssClass = null)
{
    // Short phrases repeat until they can close the ring at a readable size.
    public string RingText
    {
        get
        {
            var text = Text;

            while (text.Length < 44 && Text.Length > 0)
            {
                text += Text;
            }

            return text;
        }
    }

    // The ring is an 80-unit radius circle; Space Mono advances 0.6em per glyph
    // plus 0.12em letter-spacing, so this size makes the text close the loop.
    public string FontSize
    {
        get
        {
            var circumference = 2 * Math.PI * 80;
            var size = circumference / (Math.Max(RingText.Length, 1) * 0.72);

            return Math.Clamp(size, 8, 16).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
