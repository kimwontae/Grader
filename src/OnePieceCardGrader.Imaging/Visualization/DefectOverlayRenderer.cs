using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Visualization;

public static class DefectOverlayRenderer
{
    public static void Draw(Mat overlay, IEnumerable<DetectedDefect> defects)
    {
        foreach (var defect in defects)
        {
            if (defect.Region is null)
            {
                continue;
            }

            var x = (int)(defect.Region.X * overlay.Width);
            var y = (int)(defect.Region.Y * overlay.Height);
            var w = Math.Max(8, (int)(defect.Region.Width * overlay.Width));
            var h = Math.Max(8, (int)(defect.Region.Height * overlay.Height));
            var color = ColorFor(defect.Severity);
            Cv2.Rectangle(overlay, new Rect(x, y, w, h), color, 2);
            Cv2.PutText(
                overlay,
                LabelFor(defect),
                new Point(x, Math.Max(18, y - 6)),
                HersheyFonts.HersheySimplex,
                0.55,
                color,
                1);
        }
    }

    private static Scalar ColorFor(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Severe => new Scalar(80, 80, 220),
        DefectSeverity.Major => new Scalar(40, 120, 230),
        DefectSeverity.Moderate => new Scalar(40, 200, 230),
        DefectSeverity.Minor => new Scalar(90, 200, 90),
        _ => new Scalar(220, 170, 80)
    };

    private static string LabelFor(DetectedDefect defect)
    {
        var suffix = defect.Confidence < 0.5 ? "?" : string.Empty;
        return defect.Type switch
        {
            DefectType.CornerWhitening => "CORNER WEAR" + suffix,
            DefectType.CornerRounding => "CORNER ROUND" + suffix,
            DefectType.Scratch => "SCRATCH" + suffix,
            DefectType.Stain => "STAIN" + suffix,
            DefectType.Crease => "CREASE" + suffix,
            _ => defect.Type.ToString().ToUpperInvariant()
        };
    }
}
