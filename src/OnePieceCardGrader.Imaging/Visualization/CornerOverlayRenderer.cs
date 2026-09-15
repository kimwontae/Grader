using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Imaging.Corners;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Visualization;

public static class CornerOverlayRenderer
{
    public static Mat Render(Mat card, Rect roi, CornerContour contour, GeometryFit fit, CornerPosition position)
    {
        var overlay = card.Clone();
        using var roiView = new Mat(overlay, roi);
        using var missingTint = new Mat(roiView.Size(), roiView.Type(), new Scalar(40, 40, 220));
        using var excessTint = new Mat(roiView.Size(), roiView.Type(), new Scalar(40, 200, 40));
        Blend(roiView, missingTint, fit.MissingMask, 0.45);
        Blend(roiView, excessTint, fit.ExcessMask, 0.28);

        if (contour.Original.Length > 1)
        {
            var shifted = contour.Original.Select(p => new Point(p.X + roi.X, p.Y + roi.Y)).ToArray();
            Cv2.Polylines(overlay, new[] { shifted }, false, new Scalar(40, 220, 255), 2);
        }

        DrawLine(overlay, fit.EdgeA, roi, new Scalar(80, 200, 255));
        DrawLine(overlay, fit.EdgeB, roi, new Scalar(80, 200, 255));
        var center = ArcCenter(position, fit.Intersection, fit.RadiusPx);
        Cv2.Circle(
            overlay,
            new Point((int)(center.X + roi.X), (int)(center.Y + roi.Y)),
            (int)Math.Round(fit.RadiusPx),
            new Scalar(255, 180, 80),
            2);
        var peak = new Point((int)(fit.HighestDeviationPoint.X + roi.X), (int)(fit.HighestDeviationPoint.Y + roi.Y));
        Cv2.Circle(overlay, peak, 5, new Scalar(0, 0, 255), -1);
        Cv2.Rectangle(overlay, roi, new Scalar(220, 200, 80), 1);
        return overlay;
    }

    public static Mat DrawFittedLines(Mat roiBgr, GeometryFit fit)
    {
        var vis = roiBgr.Clone();
        DrawLine(vis, fit.EdgeA, new Rect(0, 0, vis.Width, vis.Height), new Scalar(80, 220, 255));
        DrawLine(vis, fit.EdgeB, new Rect(0, 0, vis.Width, vis.Height), new Scalar(80, 220, 255));
        Cv2.Circle(vis, (Point)fit.Intersection, 4, new Scalar(0, 80, 255), -1);
        return vis;
    }

    public static Mat DrawIdealArc(Mat roiBgr, GeometryFit fit, CornerPosition position)
    {
        var vis = roiBgr.Clone();
        var center = ArcCenter(position, fit.Intersection, fit.RadiusPx);
        Cv2.Circle(vis, (Point)center, (int)Math.Round(fit.RadiusPx), new Scalar(255, 180, 80), 2);
        return vis;
    }

    public static Mat DrawMissing(Mat roiBgr, GeometryFit fit)
    {
        var vis = roiBgr.Clone();
        vis.SetTo(new Scalar(40, 40, 230), fit.MissingMask);
        vis.SetTo(new Scalar(40, 200, 40), fit.ExcessMask);
        return vis;
    }

    private static void DrawLine(Mat image, FittedEdgeLine line, Rect roi, Scalar color)
    {
        var p1 = new Point(
            (int)(line.X0 - (line.Vx * 400) + roi.X),
            (int)(line.Y0 - (line.Vy * 400) + roi.Y));
        var p2 = new Point(
            (int)(line.X0 + (line.Vx * 400) + roi.X),
            (int)(line.Y0 + (line.Vy * 400) + roi.Y));
        Cv2.Line(image, p1, p2, color, 1);
    }

    private static Point2f ArcCenter(CornerPosition position, Point2f intersection, double radius) => position switch
    {
        CornerPosition.TopLeft => new Point2f(intersection.X + (float)radius, intersection.Y + (float)radius),
        CornerPosition.TopRight => new Point2f(intersection.X - (float)radius, intersection.Y + (float)radius),
        CornerPosition.BottomRight => new Point2f(intersection.X - (float)radius, intersection.Y - (float)radius),
        _ => new Point2f(intersection.X + (float)radius, intersection.Y - (float)radius)
    };

    private static void Blend(Mat dst, Mat tint, Mat mask, double alpha)
    {
        using var blended = new Mat();
        Cv2.AddWeighted(dst, 1.0 - alpha, tint, alpha, 0, blended);
        blended.CopyTo(dst, mask);
    }
}
