using OnePieceCardGrader.Imaging.Surface;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Visualization;

public static class ScratchOverlayRenderer
{
    public static Mat Render(Mat bgr, IReadOnlyList<ScratchComponentMetrics> candidates, Mat glareMask)
    {
        var overlay = bgr.Clone();
        using var glareTint = new Mat(overlay.Size(), overlay.Type(), new Scalar(80, 80, 220));
        using var blended = new Mat();
        Cv2.AddWeighted(overlay, 0.82, glareTint, 0.18, 0, blended);
        blended.CopyTo(overlay, glareMask);

        var index = 1;
        foreach (var candidate in candidates.OrderByDescending(c => c.Severity))
        {
            var color = candidate.GlareOverlap > 0.3
                ? new Scalar(180, 180, 80)
                : new Scalar(40, 90, 255);
            Cv2.Rectangle(overlay, candidate.BoundingBox, color, 2);
            Cv2.PutText(
                overlay,
                $"S{index}",
                new Point(candidate.BoundingBox.X, Math.Max(16, candidate.BoundingBox.Y - 4)),
                HersheyFonts.HersheySimplex,
                0.5,
                color,
                1);
            index++;
        }

        return overlay;
    }

    public static Mat RenderCandidates(Mat bgr, IReadOnlyList<ScratchComponentMetrics> candidates)
    {
        var vis = bgr.Clone();
        foreach (var candidate in candidates)
        {
            Cv2.Rectangle(vis, candidate.BoundingBox, new Scalar(40, 220, 255), 2);
        }

        return vis;
    }
}
