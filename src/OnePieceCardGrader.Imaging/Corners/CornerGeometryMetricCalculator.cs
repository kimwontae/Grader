using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Corners;

public sealed class CornerGeometryMetricCalculator
{
    public GeometryFit Fit(Mat cardMaskRoi, CornerContour contour, CornerPosition position, int cardWidth, CornerGeometryAnalysisOptions options)
    {
        var size = cardMaskRoi.Size();
        var (edgeA, edgeB) = CollectStableEdgePoints(cardMaskRoi, position, options.StableEdgeInsetRatio);
        var lineA = FitOrFallback(edgeA, position, size, primary: true);
        var lineB = FitOrFallback(edgeB, position, size, primary: false);
        var intersection = Intersect(lineA, lineB) ?? FallbackIntersection(position, size);
        var idealOrigin = FallbackIntersection(position, size);
        var expectedRadius = Math.Max(4.0, options.DefaultCornerRadiusRatio * cardWidth);
        var measuredRadius = EstimateRadius(contour.Smoothed, intersection, expectedRadius, size);
        var radius = expectedRadius;

        using var ideal = BuildIdealMask(size, position, idealOrigin, radius);
        using var missing = new Mat();
        using var excess = new Mat();
        using var invertedMask = new Mat();
        Cv2.BitwiseNot(cardMaskRoi, invertedMask);
        using var invertedIdeal = new Mat();
        Cv2.BitwiseNot(ideal, invertedIdeal);
        Cv2.BitwiseAnd(ideal, invertedMask, missing);
        Cv2.BitwiseAnd(cardMaskRoi, invertedIdeal, excess);
        var idealArea = Math.Max(1, Cv2.CountNonZero(ideal));
        var missingRatio = Cv2.CountNonZero(missing) / (double)idealArea;
        var excessRatio = Cv2.CountNonZero(excess) / (double)idealArea;

        var deviations = MeasureDeviations(contour.Smoothed, position, intersection, radius, cardWidth);
        var roughness = MeasureRoughness(contour.Smoothed);
        var radiusDeviation = Math.Abs(measuredRadius - expectedRadius) / Math.Max(1.0, cardWidth);

        var highest = deviations.HighestPoint;
        return new GeometryFit(
            lineA,
            lineB,
            intersection,
            radius,
            expectedRadius / cardWidth,
            measuredRadius / cardWidth,
            radiusDeviation,
            deviations.Mean,
            deviations.Max,
            missingRatio,
            excessRatio,
            roughness.Mean,
            roughness.Std,
            roughness.HighFrequency,
            roughness.Continuity,
            ideal.Clone(),
            missing.Clone(),
            excess.Clone(),
            highest);
    }

    private static (List<Point2f> A, List<Point2f> B) CollectStableEdgePoints(Mat mask, CornerPosition position, double insetRatio)
    {
        var a = new List<Point2f>();
        var b = new List<Point2f>();
        var w = mask.Width;
        var h = mask.Height;
        var insetX = Math.Max(6, (int)(w * Math.Min(insetRatio, 0.12)));
        var insetY = Math.Max(6, (int)(h * Math.Min(insetRatio, 0.12)));
        var indexer = mask.GetGenericIndexer<byte>();

        for (var x = 0; x < w; x++)
        {
            if (position is CornerPosition.TopLeft or CornerPosition.TopRight && x > insetX && x < w - insetX)
            {
                for (var y = 0; y < h; y++)
                {
                    if (indexer[y, x] != 0)
                    {
                        a.Add(new Point2f(x, y));
                        break;
                    }
                }
            }

            if (position is CornerPosition.BottomLeft or CornerPosition.BottomRight && x > insetX && x < w - insetX)
            {
                for (var y = h - 1; y >= 0; y--)
                {
                    if (indexer[y, x] != 0)
                    {
                        a.Add(new Point2f(x, y));
                        break;
                    }
                }
            }
        }

        for (var y = 0; y < h; y++)
        {
            if (position is CornerPosition.TopLeft or CornerPosition.BottomLeft && y > insetY && y < h - insetY)
            {
                for (var x = 0; x < w; x++)
                {
                    if (indexer[y, x] != 0)
                    {
                        b.Add(new Point2f(x, y));
                        break;
                    }
                }
            }

            if (position is CornerPosition.TopRight or CornerPosition.BottomRight && y > insetY && y < h - insetY)
            {
                for (var x = w - 1; x >= 0; x--)
                {
                    if (indexer[y, x] != 0)
                    {
                        b.Add(new Point2f(x, y));
                        break;
                    }
                }
            }
        }

        return (a, b);
    }

    private static FittedEdgeLine FitOrFallback(List<Point2f> points, CornerPosition position, Size size, bool primary)
    {
        if (points.Count >= 10)
        {
            var line = Cv2.FitLine(points.ToArray(), DistanceTypes.L2, 0, 0.01, 0.01);
            return new FittedEdgeLine(line.Vx, line.Vy, line.X1, line.Y1);
        }

        return FallbackLine(position, size, primary);
    }

    private static FittedEdgeLine FallbackLine(CornerPosition position, Size size, bool primary) =>
        (position, primary) switch
        {
            (CornerPosition.TopLeft, true) => new FittedEdgeLine(1, 0, 0, 0),
            (CornerPosition.TopLeft, false) => new FittedEdgeLine(0, 1, 0, 0),
            (CornerPosition.TopRight, true) => new FittedEdgeLine(1, 0, size.Width - 1, 0),
            (CornerPosition.TopRight, false) => new FittedEdgeLine(0, 1, size.Width - 1, 0),
            (CornerPosition.BottomRight, true) => new FittedEdgeLine(1, 0, size.Width - 1, size.Height - 1),
            (CornerPosition.BottomRight, false) => new FittedEdgeLine(0, 1, size.Width - 1, size.Height - 1),
            (CornerPosition.BottomLeft, true) => new FittedEdgeLine(1, 0, 0, size.Height - 1),
            _ => new FittedEdgeLine(0, 1, 0, size.Height - 1)
        };

    private static Point2f? Intersect(FittedEdgeLine a, FittedEdgeLine b)
    {
        var dx = a.Vx;
        var dy = a.Vy;
        var ex = b.Vx;
        var ey = b.Vy;
        var det = (dx * ey) - (dy * ex);
        if (Math.Abs(det) < 1e-6)
        {
            return null;
        }

        var t = (((b.X0 - a.X0) * ey) - ((b.Y0 - a.Y0) * ex)) / det;
        return new Point2f((float)(a.X0 + (t * dx)), (float)(a.Y0 + (t * dy)));
    }

    private static Point2f FallbackIntersection(CornerPosition position, Size size) => position switch
    {
        CornerPosition.TopLeft => new Point2f(0, 0),
        CornerPosition.TopRight => new Point2f(size.Width - 1, 0),
        CornerPosition.BottomRight => new Point2f(size.Width - 1, size.Height - 1),
        _ => new Point2f(0, size.Height - 1)
    };

    private static double EstimateRadius(Point2f[] contour, Point2f intersection, double expected, Size size)
    {
        var arc = contour
            .Select(p => (Point: p, Dist: Distance(p, intersection)))
            .Where(x => x.Dist > expected * 0.25 && x.Dist < expected * 2.8)
            .Select(x => x.Point)
            .ToArray();
        if (arc.Length >= 5)
        {
            try
            {
                var ellipse = Cv2.FitEllipse(arc);
                return Math.Clamp((ellipse.Size.Width + ellipse.Size.Height) / 4.0, 2, Math.Min(size.Width, size.Height) * 0.6);
            }
            catch (OpenCVException)
            {
                return expected;
            }
        }

        if (arc.Length >= 3)
        {
            Cv2.MinEnclosingCircle(arc, out _, out var radius);
            return radius;
        }

        return expected;
    }

    private static Mat BuildIdealMask(Size size, CornerPosition position, Point2f intersection, double radius)
    {
        var mask = new Mat(size, MatType.CV_8UC1, Scalar.All(255));
        var r = (float)radius;
        var center = position switch
        {
            CornerPosition.TopLeft => new Point2f(intersection.X + r, intersection.Y + r),
            CornerPosition.TopRight => new Point2f(intersection.X - r, intersection.Y + r),
            CornerPosition.BottomRight => new Point2f(intersection.X - r, intersection.Y - r),
            _ => new Point2f(intersection.X + r, intersection.Y - r)
        };

        using var cut = new Mat(size, MatType.CV_8UC1, Scalar.All(255));
        switch (position)
        {
            case CornerPosition.TopLeft:
                Cv2.Rectangle(cut, new Rect(0, 0, Math.Max(1, (int)center.X), Math.Max(1, (int)center.Y)), Scalar.All(0), -1);
                break;
            case CornerPosition.TopRight:
                Cv2.Rectangle(cut, new Rect((int)center.X, 0, Math.Max(1, size.Width - (int)center.X), Math.Max(1, (int)center.Y)), Scalar.All(0), -1);
                break;
            case CornerPosition.BottomRight:
                Cv2.Rectangle(cut, new Rect((int)center.X, (int)center.Y, Math.Max(1, size.Width - (int)center.X), Math.Max(1, size.Height - (int)center.Y)), Scalar.All(0), -1);
                break;
            default:
                Cv2.Rectangle(cut, new Rect(0, (int)center.Y, Math.Max(1, (int)center.X), Math.Max(1, size.Height - (int)center.Y)), Scalar.All(0), -1);
                break;
        }

        using var disk = new Mat(size, MatType.CV_8UC1, Scalar.All(0));
        Cv2.Circle(disk, (Point)center, (int)Math.Round(radius), Scalar.All(255), -1);
        using var rounded = new Mat();
        Cv2.BitwiseOr(cut, disk, rounded);
        Cv2.BitwiseAnd(mask, rounded, mask);
        return mask;
    }

    private static DeviationSet MeasureDeviations(
        Point2f[] contour,
        CornerPosition position,
        Point2f intersection,
        double radius,
        int cardWidth)
    {
        if (contour.Length == 0)
        {
            return new DeviationSet(0, 0, intersection);
        }

        var center = ArcCenter(position, intersection, radius);
        double sum = 0, max = 0;
        var highest = contour[0];
        foreach (var point in contour)
        {
            var inArc = IsInArcSector(position, point, center, radius);
            var distance = inArc
                ? Math.Abs(Distance(point, center) - radius)
                : DistanceToOuterEdges(position, point, intersection);
            var normalized = distance / Math.Max(1, cardWidth);
            sum += normalized;
            if (normalized > max)
            {
                max = normalized;
                highest = point;
            }
        }

        return new DeviationSet(sum / contour.Length, max, highest);
    }

    private static RoughnessSet MeasureRoughness(Point2f[] contour)
    {
        if (contour.Length < 6)
        {
            return new RoughnessSet(0, 0, 0, 1);
        }

        var curvatures = new double[contour.Length];
        for (var i = 1; i < contour.Length - 1; i++)
        {
            var v1x = contour[i].X - contour[i - 1].X;
            var v1y = contour[i].Y - contour[i - 1].Y;
            var v2x = contour[i + 1].X - contour[i].X;
            var v2y = contour[i + 1].Y - contour[i].Y;
            var len1 = Math.Max(1e-6, Math.Sqrt((v1x * v1x) + (v1y * v1y)));
            var len2 = Math.Max(1e-6, Math.Sqrt((v2x * v2x) + (v2y * v2y)));
            var cross = (v1x * v2y) - (v1y * v2x);
            var dot = (v1x * v2x) + (v1y * v2y);
            var angle = Math.Atan2(cross, dot);
            curvatures[i] = angle / ((len1 + len2) * 0.5);
        }

        var usable = curvatures.Skip(1).Take(curvatures.Length - 2).ToArray();
        var mean = usable.Average();
        var std = Math.Sqrt(usable.Average(v => (v - mean) * (v - mean)));
        var lowpass = MovingAverage(usable, 7);
        var energy = usable.Select((v, i) => v - lowpass[i]).Average(v => v * v);
        var continuity = 1.0 / (1.0 + (std * 40.0));
        return new RoughnessSet(mean, std, energy, continuity);
    }

    private static double[] MovingAverage(double[] values, int window)
    {
        var half = window / 2;
        var result = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            double sum = 0;
            var n = 0;
            for (var k = -half; k <= half; k++)
            {
                var idx = Math.Clamp(i + k, 0, values.Length - 1);
                sum += values[idx];
                n++;
            }

            result[i] = sum / n;
        }

        return result;
    }

    private static Point2f ArcCenter(CornerPosition position, Point2f intersection, double radius) => position switch
    {
        CornerPosition.TopLeft => new Point2f(intersection.X + (float)radius, intersection.Y + (float)radius),
        CornerPosition.TopRight => new Point2f(intersection.X - (float)radius, intersection.Y + (float)radius),
        CornerPosition.BottomRight => new Point2f(intersection.X - (float)radius, intersection.Y - (float)radius),
        _ => new Point2f(intersection.X + (float)radius, intersection.Y - (float)radius)
    };

    private static bool IsInArcSector(CornerPosition position, Point2f point, Point2f center, double radius)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return position switch
        {
            CornerPosition.TopLeft => dx <= 0 && dy <= 0 && Distance(point, center) < radius * 2,
            CornerPosition.TopRight => dx >= 0 && dy <= 0 && Distance(point, center) < radius * 2,
            CornerPosition.BottomRight => dx >= 0 && dy >= 0 && Distance(point, center) < radius * 2,
            _ => dx <= 0 && dy >= 0 && Distance(point, center) < radius * 2
        };
    }

    private static double DistanceToOuterEdges(CornerPosition position, Point2f point, Point2f intersection) =>
        position switch
        {
            CornerPosition.TopLeft => Math.Min(Math.Abs(point.Y - intersection.Y), Math.Abs(point.X - intersection.X)),
            CornerPosition.TopRight => Math.Min(Math.Abs(point.Y - intersection.Y), Math.Abs(point.X - intersection.X)),
            CornerPosition.BottomRight => Math.Min(Math.Abs(point.Y - intersection.Y), Math.Abs(point.X - intersection.X)),
            _ => Math.Min(Math.Abs(point.Y - intersection.Y), Math.Abs(point.X - intersection.X))
        };

    private static double Distance(Point2f a, Point2f b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private readonly record struct DeviationSet(double Mean, double Max, Point2f HighestPoint);
    private readonly record struct RoughnessSet(double Mean, double Std, double HighFrequency, double Continuity);
}

public readonly record struct FittedEdgeLine(double Vx, double Vy, double X0, double Y0);

public sealed record GeometryFit(
    FittedEdgeLine EdgeA,
    FittedEdgeLine EdgeB,
    Point2f Intersection,
    double RadiusPx,
    double ExpectedRadiusRatio,
    double MeasuredRadiusRatio,
    double RadiusDeviation,
    double MeanDeviation,
    double MaxDeviation,
    double MissingAreaRatio,
    double ExcessAreaRatio,
    double CurvatureMean,
    double CurvatureStdDev,
    double HighFrequencyEnergy,
    double Continuity,
    Mat IdealMask,
    Mat MissingMask,
    Mat ExcessMask,
    Point2f HighestDeviationPoint) : IDisposable
{
    public void Dispose()
    {
        IdealMask.Dispose();
        MissingMask.Dispose();
        ExcessMask.Dispose();
    }
}
