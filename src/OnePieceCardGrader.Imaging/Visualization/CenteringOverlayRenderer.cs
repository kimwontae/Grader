using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Visualization;

public static class CenteringOverlayRenderer
{
    public static Mat Render(Mat normalized, CenteringMeasurement measurement, Scalar lineColor)
    {
        var overlay = normalized.Clone();
        var w = overlay.Width;
        var h = overlay.Height;
        var guide = measurement.Guide ?? CenteringGuide.CreateDefault();
        var left = (int)Math.Round(guide.PrintLeft * w);
        var right = (int)Math.Round(guide.PrintRight * w);
        var top = (int)Math.Round(guide.PrintTop * h);
        var bottom = (int)Math.Round(guide.PrintBottom * h);

        Cv2.Line(overlay, new Point(left, 0), new Point(left, h), lineColor, 2);
        Cv2.Line(overlay, new Point(right, 0), new Point(right, h), lineColor, 2);
        Cv2.Line(overlay, new Point(0, top), new Point(w, top), lineColor, 2);
        Cv2.Line(overlay, new Point(0, bottom), new Point(w, bottom), lineColor, 2);
        Cv2.Rectangle(overlay, new Point(0, 0), new Point(w - 1, h - 1), new Scalar(40, 180, 255), 2);

        var label = $"L/R {measurement.LeftPercent:F1}/{measurement.RightPercent:F1}  T/B {measurement.TopPercent:F1}/{measurement.BottomPercent:F1}";
        Cv2.PutText(overlay, label, new Point(24, 42), HersheyFonts.HersheySimplex, 1.0, lineColor, 2);
        return overlay;
    }
}
