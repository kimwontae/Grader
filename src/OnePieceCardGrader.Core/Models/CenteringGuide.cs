namespace OnePieceCardGrader.Core.Models;

/// <summary>
/// Print-frame guide in normalized 0..1 coordinates of the card image.
/// </summary>
public sealed class CenteringGuide
{
    public double PrintLeft { get; set; }
    public double PrintRight { get; set; }
    public double PrintTop { get; set; }
    public double PrintBottom { get; set; }

    public static CenteringGuide CreateDefault() => new()
    {
        PrintLeft = 0.04,
        PrintRight = 0.96,
        PrintTop = 0.04,
        PrintBottom = 0.96
    };

    public CenteringGuide Clone() => new()
    {
        PrintLeft = PrintLeft,
        PrintRight = PrintRight,
        PrintTop = PrintTop,
        PrintBottom = PrintBottom
    };
}
