using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Quality;

public sealed class GlareAnalysis
{
    public double CoverageRatio { get; init; }
    public Mat? Mask { get; init; }
}

public static class GlareDetector
{
    public static GlareAnalysis Analyze(Mat bgr, QualityOptions options)
    {
        using var hsv = bgr.CvtColor(ColorConversionCodes.BGR2HSV);
        var channels = hsv.Split();
        using var saturation = channels[1];
        using var value = channels[2];
        foreach (var channel in channels)
        {
            if (!ReferenceEquals(channel, saturation) && !ReferenceEquals(channel, value))
            {
                channel.Dispose();
            }
        }

        using var lowSat = new Mat();
        using var highVal = new Mat();
        Cv2.Threshold(saturation, lowSat, options.GlareSaturationThreshold, 255, ThresholdTypes.BinaryInv);
        Cv2.Threshold(value, highVal, options.GlareValueThreshold, 255, ThresholdTypes.Binary);

        using var hsvMask = new Mat();
        Cv2.BitwiseAnd(lowSat, highVal, hsvMask);

        using var gray = bgr.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var saturated = new Mat();
        Cv2.Threshold(gray, saturated, 252, 255, ThresholdTypes.Binary);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using var local = saturated.MorphologyEx(MorphTypes.Open, kernel);

        var mask = new Mat();
        Cv2.BitwiseOr(hsvMask, local, mask);
        var coverage = Cv2.CountNonZero(mask) / (double)(mask.Rows * mask.Cols);

        return new GlareAnalysis
        {
            CoverageRatio = coverage,
            Mask = mask
        };
    }
}
