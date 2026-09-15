using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Corners;

public sealed class CornerContourDetector
{
    public CornerContour Extract(Mat cardMaskRoi, CornerPosition position, CornerGeometryAnalysisOptions options)
    {
        using var work = cardMaskRoi.Clone();
        Cv2.FindContours(work, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxNone);
        if (contours.Length == 0)
        {
            return CornerContour.Empty(position);
        }

        var largest = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        var raw = SampleByArcLength(largest, options.ContourSampleCount);
        var smoothed = Smooth(raw, options.SmoothingWindow);
        return new CornerContour(position, raw, smoothed, largest);
    }

    private static Point2f[] SampleByArcLength(Point[] contour, int count)
    {
        if (contour.Length == 0)
        {
            return [];
        }

        var samples = Math.Max(16, count);
        var pts = contour.Select(p => new Point2f(p.X, p.Y)).ToArray();
        using var mat = InputArray.Create(pts);
        var length = Cv2.ArcLength(pts, false);
        if (length <= 1)
        {
            return pts;
        }

        var result = new List<Point2f>(samples);
        var step = length / (samples - 1);
        var accumulated = 0.0;
        var target = 0.0;
        result.Add(pts[0]);
        for (var i = 1; i < pts.Length && result.Count < samples; i++)
        {
            var dx = pts[i].X - pts[i - 1].X;
            var dy = pts[i].Y - pts[i - 1].Y;
            var seg = Math.Sqrt((dx * dx) + (dy * dy));
            while (accumulated + seg >= target && result.Count < samples && seg > 0)
            {
                var t = (target - accumulated) / seg;
                result.Add(new Point2f(
                    (float)(pts[i - 1].X + (dx * t)),
                    (float)(pts[i - 1].Y + (dy * t))));
                target += step;
            }

            accumulated += seg;
        }

        if (result.Count < samples)
        {
            result.Add(pts[^1]);
        }

        return result.ToArray();
    }

    private static Point2f[] Smooth(Point2f[] points, int window)
    {
        if (points.Length == 0)
        {
            return points;
        }

        var w = Math.Max(1, window);
        if (w % 2 == 0)
        {
            w++;
        }

        var half = w / 2;
        var smoothed = new Point2f[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            float sx = 0, sy = 0;
            var n = 0;
            for (var k = -half; k <= half; k++)
            {
                var idx = Math.Clamp(i + k, 0, points.Length - 1);
                sx += points[idx].X;
                sy += points[idx].Y;
                n++;
            }

            smoothed[i] = new Point2f(sx / n, sy / n);
        }

        return smoothed;
    }
}

public sealed class CornerContour
{
    public CornerContour(CornerPosition position, Point2f[] raw, Point2f[] smoothed, Point[] original)
    {
        Position = position;
        Raw = raw;
        Smoothed = smoothed;
        Original = original;
    }

    public CornerPosition Position { get; }
    public Point2f[] Raw { get; }
    public Point2f[] Smoothed { get; }
    public Point[] Original { get; }
    public bool IsEmpty => Raw.Length == 0;

    public static CornerContour Empty(CornerPosition position) => new(position, [], [], []);
}
