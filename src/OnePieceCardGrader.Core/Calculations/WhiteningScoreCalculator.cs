using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Core.Calculations;

public static class WhiteningScoreCalculator
{
    public static double ComputeSeverity(
        double lengthRatio,
        double areaRatio,
        double meanDeltaE,
        double largestDefectRatio,
        WhiteningAnalysisOptions options)
    {
        var lengthSeverity = MetricNormalization.NormalizeWithReference(lengthRatio, options.SevereLengthRatio);
        var areaSeverity = MetricNormalization.NormalizeWithReference(areaRatio, options.SevereAreaRatio);
        var contrastSeverity = MetricNormalization.Normalize(meanDeltaE, options.MinDeltaE, options.StrongDeltaE);
        var largestDefectSeverity = MetricNormalization.NormalizeWithReference(
            largestDefectRatio,
            options.SevereLargestDefectRatio);

        return MetricNormalization.Clamp01(
            (lengthSeverity * options.LengthWeight)
            + (areaSeverity * options.AreaWeight)
            + (contrastSeverity * options.ContrastWeight)
            + (largestDefectSeverity * options.LargestDefectWeight));
    }

    public static double ComputeConditionScore(double severity, double largestDefectSeverity, WhiteningAnalysisOptions options)
    {
        var penalty = MetricNormalization.Clamp01(severity) * options.ConditionPenaltyScale;
        if (largestDefectSeverity > options.LargestDefectPenaltyThreshold)
        {
            var extra = (largestDefectSeverity - options.LargestDefectPenaltyThreshold)
                        / Math.Max(1.0 - options.LargestDefectPenaltyThreshold, 0.0001)
                        * options.LargestDefectExtraPenalty;
            penalty += extra;
        }

        return Math.Clamp(100.0 - penalty, 0.0, 100.0);
    }

    public static double ComputePixelScore(
        double deltaE,
        double brightnessIncrease,
        double saturationDecrease,
        WhiteningAnalysisOptions options)
    {
        var color = MetricNormalization.Normalize(deltaE, options.MinDeltaE, options.StrongDeltaE);
        var brightness = MetricNormalization.Normalize(
            brightnessIncrease,
            options.MinBrightnessIncrease,
            options.StrongBrightnessIncrease);
        var saturation = MetricNormalization.Normalize(
            saturationDecrease,
            options.MinSaturationDecrease,
            options.StrongSaturationDecrease);

        return MetricNormalization.Clamp01(
            (color * options.ColorDifferenceScoreWeight)
            + (brightness * options.BrightnessScoreWeight)
            + (saturation * options.SaturationScoreWeight));
    }

    public static double ComputeConfidence(
        double imageQuality,
        double referenceStability,
        double glareQuality,
        double detectionQuality,
        WhiteningAnalysisOptions options)
    {
        var value =
            (MetricNormalization.Clamp01(imageQuality / 100.0) * options.ImageQualityWeight)
            + (MetricNormalization.Clamp01(referenceStability) * options.ReferenceStabilityWeight)
            + (MetricNormalization.Clamp01(glareQuality) * options.GlareQualityWeight)
            + (MetricNormalization.Clamp01(detectionQuality) * options.DetectionQualityWeight);

        return Math.Clamp(value * 100.0, 0.0, 100.0);
    }
}
