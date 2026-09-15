namespace OnePieceCardGrader.Core.Models;

/// <summary>
/// Rectangle in normalized 0..1 coordinates relative to the card image.
/// </summary>
public sealed class NormalizedRect
{
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public static NormalizedRect FromPixels(double x, double y, double width, double height, double imageWidth, double imageHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Image dimensions must be positive.");
        }

        return new NormalizedRect
        {
            X = x / imageWidth,
            Y = y / imageHeight,
            Width = width / imageWidth,
            Height = height / imageHeight
        };
    }
}
