using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Visualization;

public static class DefectZoomRenderer
{
    public static Mat? Crop(Mat source, NormalizedRect region, string label)
    {
        if (source.Empty() || region.Width <= 0 || region.Height <= 0)
        {
            return null;
        }

        var pad = 1.4;
        var width = Math.Clamp(region.Width * pad, 0.08, 1.0);
        var height = Math.Clamp(region.Height * pad, 0.08, 1.0);
        var x = Math.Clamp(region.X + (region.Width / 2.0) - (width / 2.0), 0, 1 - width);
        var y = Math.Clamp(region.Y + (region.Height / 2.0) - (height / 2.0), 0, 1 - height);

        var px = (int)Math.Floor(x * source.Width);
        var py = (int)Math.Floor(y * source.Height);
        var pw = Math.Max(32, (int)Math.Ceiling(width * source.Width));
        var ph = Math.Max(32, (int)Math.Ceiling(height * source.Height));
        pw = Math.Min(pw, source.Width - px);
        ph = Math.Min(ph, source.Height - py);
        if (pw <= 8 || ph <= 8)
        {
            return null;
        }

        using var cropped = new Mat(source, new Rect(px, py, pw, ph));
        var zoom = cropped.Clone();
        var scale = Math.Max(1.0, 360.0 / Math.Min(zoom.Width, zoom.Height));
        if (scale > 1.01)
        {
            var resized = zoom.Resize(new Size((int)(zoom.Width * scale), (int)(zoom.Height * scale)), 0, 0, InterpolationFlags.Cubic);
            zoom.Dispose();
            zoom = resized;
        }

        var boxX = (int)Math.Round((region.X - x) / width * zoom.Width);
        var boxY = (int)Math.Round((region.Y - y) / height * zoom.Height);
        var boxW = Math.Max(8, (int)Math.Round(region.Width / width * zoom.Width));
        var boxH = Math.Max(8, (int)Math.Round(region.Height / height * zoom.Height));
        boxX = Math.Clamp(boxX, 0, Math.Max(0, zoom.Width - 8));
        boxY = Math.Clamp(boxY, 0, Math.Max(0, zoom.Height - 8));
        boxW = Math.Min(boxW, zoom.Width - boxX);
        boxH = Math.Min(boxH, zoom.Height - boxY);
        Cv2.Rectangle(zoom, new Rect(boxX, boxY, boxW, boxH), new Scalar(40, 80, 255), 3);
        Cv2.PutText(
            zoom,
            label,
            new Point(Math.Max(8, boxX), Math.Max(28, boxY - 10)),
            HersheyFonts.HersheySimplex,
            0.7,
            new Scalar(40, 80, 255),
            2);
        return zoom;
    }
}
