namespace OnePieceCardGrader.Core.Models;

public readonly record struct ImagePoint(double X, double Y)
{
    public static ImagePoint operator +(ImagePoint a, ImagePoint b) => new(a.X + b.X, a.Y + b.Y);

    public double DistanceTo(ImagePoint other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
