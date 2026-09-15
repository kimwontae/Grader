namespace OnePieceCardGrader.Core.Models;

public sealed class QuadCorners
{
    public ImagePoint TopLeft { get; init; }
    public ImagePoint TopRight { get; init; }
    public ImagePoint BottomRight { get; init; }
    public ImagePoint BottomLeft { get; init; }

    public IReadOnlyList<ImagePoint> ToList() =>
        [TopLeft, TopRight, BottomRight, BottomLeft];

    public static QuadCorners FromUnordered(IReadOnlyList<ImagePoint> points)
    {
        if (points.Count != 4)
        {
            throw new ArgumentException("Exactly four points are required.", nameof(points));
        }

        var ordered = OrderPoints(points);
        return new QuadCorners
        {
            TopLeft = ordered[0],
            TopRight = ordered[1],
            BottomRight = ordered[2],
            BottomLeft = ordered[3]
        };
    }

    /// <summary>
    /// Orders points as TopLeft, TopRight, BottomRight, BottomLeft.
    /// </summary>
    public static ImagePoint[] OrderPoints(IReadOnlyList<ImagePoint> points)
    {
        if (points.Count != 4)
        {
            throw new ArgumentException("Exactly four points are required.", nameof(points));
        }

        var centroidX = points.Average(p => p.X);
        var centroidY = points.Average(p => p.Y);

        ImagePoint? topLeft = null;
        ImagePoint? topRight = null;
        ImagePoint? bottomRight = null;
        ImagePoint? bottomLeft = null;

        foreach (var point in points)
        {
            if (point.X <= centroidX && point.Y <= centroidY)
            {
                topLeft = SelectCloserToOrigin(topLeft, point, preferMinX: true, preferMinY: true);
            }
            else if (point.X > centroidX && point.Y <= centroidY)
            {
                topRight = SelectCloserToOrigin(topRight, point, preferMinX: false, preferMinY: true);
            }
            else if (point.X > centroidX && point.Y > centroidY)
            {
                bottomRight = SelectCloserToOrigin(bottomRight, point, preferMinX: false, preferMinY: false);
            }
            else
            {
                bottomLeft = SelectCloserToOrigin(bottomLeft, point, preferMinX: true, preferMinY: false);
            }
        }

        if (topLeft is null || topRight is null || bottomRight is null || bottomLeft is null)
        {
            return OrderBySumDifference(points);
        }

        return [topLeft.Value, topRight.Value, bottomRight.Value, bottomLeft.Value];
    }

    private static ImagePoint SelectCloserToOrigin(ImagePoint? existing, ImagePoint candidate, bool preferMinX, bool preferMinY)
    {
        if (existing is null)
        {
            return candidate;
        }

        var current = existing.Value;
        var currentScore = (preferMinX ? current.X : -current.X) + (preferMinY ? current.Y : -current.Y);
        var candidateScore = (preferMinX ? candidate.X : -candidate.X) + (preferMinY ? candidate.Y : -candidate.Y);
        return candidateScore < currentScore ? candidate : current;
    }

    private static ImagePoint[] OrderBySumDifference(IReadOnlyList<ImagePoint> points)
    {
        var bySum = points.OrderBy(p => p.X + p.Y).ToArray();
        var topLeft = bySum[0];
        var bottomRight = bySum[^1];
        var remaining = points.Where(p => !p.Equals(topLeft) && !p.Equals(bottomRight)).ToArray();
        var byDiff = remaining.OrderBy(p => p.Y - p.X).ToArray();
        var topRight = byDiff[0];
        var bottomLeft = byDiff[^1];
        return [topLeft, topRight, bottomRight, bottomLeft];
    }
}
