using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Core.Calculations;

public static class ScratchScoreCalculator
{
    public static double ComputeCandidateScore(
        double lengthScore,
        double thinnessScore,
        double contrastScore,
        double gradientScore,
        double lightingResponseScore,
        double shapeScore,
        double glareOverlap,
        ScratchAnalysisOptions options)
    {
        var raw =
            (lengthScore * options.LengthWeight)
            + (thinnessScore * options.ThinnessWeight)
            + (contrastScore * options.ContrastWeight)
            + (gradientScore * options.GradientWeight)
            + (lightingResponseScore * options.LightingWeight)
            + (shapeScore * options.ShapeWeight);

        return MetricNormalization.Clamp01(raw * (1.0 - (MetricNormalization.Clamp01(glareOverlap) * 0.7)));
    }

    public static double ComputeScratchSeverity(
        double lengthSeverity,
        double widthSeverity,
        double contrastSeverity,
        double visualImpactSeverity,
        ScratchAnalysisOptions options)
    {
        return MetricNormalization.Clamp01(
            (lengthSeverity * options.ScratchLengthSeverityWeight)
            + (widthSeverity * options.ScratchWidthSeverityWeight)
            + (contrastSeverity * options.ScratchContrastSeverityWeight)
            + (visualImpactSeverity * options.ScratchVisualImpactWeight));
    }

    public static double ComputeSurfaceSeverity(
        double worstSeverity,
        double secondWorstSeverity,
        double affectedAreaSeverity,
        double countSeverity,
        ScratchAnalysisOptions options)
    {
        return MetricNormalization.Clamp01(
            (worstSeverity * options.WorstScratchWeight)
            + (secondWorstSeverity * options.SecondWorstScratchWeight)
            + (affectedAreaSeverity * options.AffectedAreaWeight)
            + (countSeverity * options.CountWeight));
    }

    public static double ComputeConditionScore(double surfaceSeverity, ScratchAnalysisOptions options) =>
        Math.Clamp(100.0 - (MetricNormalization.Clamp01(surfaceSeverity) * options.ConditionPenaltyScale), 0.0, 100.0);

    public static double ComputeConfidence(
        double normalImageQuality,
        double angledImageQuality,
        double alignmentConfidence,
        double glareCoverage,
        double candidateShapeConfidence,
        double lightingResponseAvailability,
        ScratchAnalysisOptions options)
    {
        var glareQuality = 1.0 - MetricNormalization.Clamp01(glareCoverage);
        var value =
            (MetricNormalization.Clamp01(normalImageQuality / 100.0) * options.NormalImageQualityWeight)
            + (MetricNormalization.Clamp01(angledImageQuality / 100.0) * options.AngledImageQualityWeight)
            + (MetricNormalization.Clamp01(alignmentConfidence) * options.AlignmentConfidenceWeight)
            + (glareQuality * options.GlareCoverageWeight)
            + (MetricNormalization.Clamp01(candidateShapeConfidence) * options.ShapeConfidenceWeight)
            + (MetricNormalization.Clamp01(lightingResponseAvailability) * options.LightingAvailabilityWeight);

        return Math.Clamp(value * 100.0, 0.0, 100.0);
    }
}
