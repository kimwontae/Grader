using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Quality;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Surface;

public static class SurfaceGlareMaskBuilder
{
    public static GlareMask Build(Mat bgr, QualityOptions quality, ScratchAnalysisOptions scratch)
    {
        var baseGlare = GlareDetector.Analyze(bgr, quality);
        using var hsv = bgr.CvtColor(ColorConversionCodes.BGR2HSV);
        var channels = hsv.Split();
        using var v = channels[2];
        using var s = channels[1];
        foreach (var channel in channels)
        {
            if (!ReferenceEquals(channel, v) && !ReferenceEquals(channel, s))
            {
                channel.Dispose();
            }
        }

        using var gray = bgr.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var blur = gray.GaussianBlur(new Size(21, 21), 0);
        using var local = new Mat();
        Cv2.Subtract(gray, blur, local);
        using var hot = new Mat();
        Cv2.Threshold(local, hot, 40, 255, ThresholdTypes.Binary);
        using var highV = new Mat();
        Cv2.Threshold(v, highV, quality.GlareValueThreshold, 255, ThresholdTypes.Binary);
        using var smooth = highV.MorphologyEx(MorphTypes.Open, Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(9, 9)));
        var mask = baseGlare.Mask ?? new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
        Cv2.BitwiseOr(mask, hot, mask);
        Cv2.BitwiseOr(mask, smooth, mask);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(mask, mask, MorphTypes.Close, kernel);
        var coverage = Cv2.CountNonZero(mask) / (double)Math.Max(1, mask.Rows * mask.Cols);
        _ = scratch;
        return new GlareMask(mask, coverage);
    }
}
