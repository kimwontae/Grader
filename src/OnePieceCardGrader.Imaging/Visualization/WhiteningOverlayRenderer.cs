using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Visualization;

public static class WhiteningOverlayRenderer
{
    public static Mat Render(
        Mat card,
        Rect roi,
        Mat candidateZone,
        Mat referenceZone,
        Mat whiteningMask,
        IReadOnlyList<WhiteningComponent> components,
        WhiteningRegion region)
    {
        var overlay = card.Clone();
        Cv2.Rectangle(overlay, roi, new Scalar(220, 200, 80), 2);

        using var roiView = new Mat(overlay, roi);
        using var refTint = new Mat(roiView.Size(), roiView.Type(), new Scalar(40, 160, 60));
        using var candTint = new Mat(roiView.Size(), roiView.Type(), new Scalar(40, 200, 255));
        using var whiteTint = new Mat(roiView.Size(), roiView.Type(), new Scalar(90, 90, 255));
        Blend(roiView, refTint, referenceZone, 0.28);
        Blend(roiView, candTint, candidateZone, 0.22);
        Blend(roiView, whiteTint, whiteningMask, 0.45);

        foreach (var component in components)
        {
            var box = ToRect(component.BoundingBox, overlay.Width, overlay.Height);
            Cv2.Rectangle(overlay, box, new Scalar(30, 60, 255), 2);
            Cv2.PutText(
                overlay,
                $"W{component.Id}",
                new Point(box.X, Math.Max(16, box.Y - 4)),
                HersheyFonts.HersheySimplex,
                0.5,
                new Scalar(30, 60, 255),
                1);
        }

        Cv2.PutText(
            overlay,
            region.ToString(),
            new Point(roi.X + 8, roi.Y + 22),
            HersheyFonts.HersheySimplex,
            0.55,
            new Scalar(240, 240, 240),
            1);
        return overlay;
    }

    public static Mat RenderComponents(Mat roiBgr, IReadOnlyList<WhiteningComponent> components, Rect roi, Size cardSize)
    {
        var vis = roiBgr.Clone();
        foreach (var component in components)
        {
            var full = ToRect(component.BoundingBox, cardSize.Width, cardSize.Height);
            var box = new Rect(full.X - roi.X, full.Y - roi.Y, full.Width, full.Height);
            box.X = Math.Clamp(box.X, 0, Math.Max(0, vis.Width - 1));
            box.Y = Math.Clamp(box.Y, 0, Math.Max(0, vis.Height - 1));
            box.Width = Math.Clamp(box.Width, 1, vis.Width - box.X);
            box.Height = Math.Clamp(box.Height, 1, vis.Height - box.Y);
            Cv2.Rectangle(vis, box, new Scalar(20, 80, 255), 2);
            Cv2.PutText(
                vis,
                $"W{component.Id}",
                new Point(box.X, Math.Max(14, box.Y - 3)),
                HersheyFonts.HersheySimplex,
                0.45,
                new Scalar(20, 80, 255),
                1);
        }

        return vis;
    }

    private static void Blend(Mat dst, Mat tint, Mat mask, double alpha)
    {
        using var blended = new Mat();
        Cv2.AddWeighted(dst, 1.0 - alpha, tint, alpha, 0, blended);
        blended.CopyTo(dst, mask);
    }

    private static Rect ToRect(NormalizedRect rect, int width, int height)
    {
        var x = Math.Clamp((int)Math.Round(rect.X * width), 0, Math.Max(0, width - 1));
        var y = Math.Clamp((int)Math.Round(rect.Y * height), 0, Math.Max(0, height - 1));
        var w = Math.Clamp((int)Math.Round(rect.Width * width), 1, width - x);
        var h = Math.Clamp((int)Math.Round(rect.Height * height), 1, height - y);
        return new Rect(x, y, w, h);
    }
}
