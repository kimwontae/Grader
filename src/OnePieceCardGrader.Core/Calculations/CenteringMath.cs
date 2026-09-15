using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Core.Calculations;

public static class CenteringMath
{
    public static CenteringMeasurement FromMargins(
        double leftMargin,
        double rightMargin,
        double topMargin,
        double bottomMargin,
        double confidence,
        CenteringGuide? guide = null)
    {
        var horizontal = Math.Max(leftMargin, 0) + Math.Max(rightMargin, 0);
        var vertical = Math.Max(topMargin, 0) + Math.Max(bottomMargin, 0);

        var leftPercent = horizontal <= double.Epsilon
            ? AppConstants.PerfectCenteringPercent
            : leftMargin / horizontal * 100.0;
        var rightPercent = horizontal <= double.Epsilon
            ? AppConstants.PerfectCenteringPercent
            : rightMargin / horizontal * 100.0;
        var topPercent = vertical <= double.Epsilon
            ? AppConstants.PerfectCenteringPercent
            : topMargin / vertical * 100.0;
        var bottomPercent = vertical <= double.Epsilon
            ? AppConstants.PerfectCenteringPercent
            : bottomMargin / vertical * 100.0;

        var worst = Math.Max(
            Math.Max(leftPercent, rightPercent),
            Math.Max(topPercent, bottomPercent));

        return new CenteringMeasurement
        {
            LeftPercent = leftPercent,
            RightPercent = rightPercent,
            TopPercent = topPercent,
            BottomPercent = bottomPercent,
            WorstRatio = worst,
            Confidence = Math.Clamp(confidence, 0, 1),
            LeftMarginPx = leftMargin,
            RightMarginPx = rightMargin,
            TopMarginPx = topMargin,
            BottomMarginPx = bottomMargin,
            Guide = guide
        };
    }

    public static CenteringMeasurement FromGuide(CenteringGuide guide, double imageWidth, double imageHeight, double confidence)
    {
        ArgumentNullException.ThrowIfNull(guide);
        if (imageWidth <= 0 || imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth), "Image size must be positive.");
        }

        var left = guide.PrintLeft * imageWidth;
        var right = (1.0 - guide.PrintRight) * imageWidth;
        var top = guide.PrintTop * imageHeight;
        var bottom = (1.0 - guide.PrintBottom) * imageHeight;
        return FromMargins(left, right, top, bottom, confidence, guide);
    }

    public static double ConditionScore(double worstRatio)
    {
        var offset = Math.Abs(worstRatio - AppConstants.PerfectCenteringPercent);
        return Math.Clamp(100.0 - (offset * 3.2), 0, 100);
    }
}
