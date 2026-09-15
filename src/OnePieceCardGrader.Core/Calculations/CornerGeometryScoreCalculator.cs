using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Core.Calculations;

public static class CornerGeometryScoreCalculator
{
    public static double ComputeSeverity(
        double meanContourDeviation,
        double missingAreaRatio,
        double radiusDeviation,
        double roughness,
        CornerGeometryAnalysisOptions options)
    {
        var deviationSeverity = MetricNormalization.NormalizeWithReference(
            meanContourDeviation,
            options.MaxNormalDeviationRatio);
        var missingAreaSeverity = MetricNormalization.NormalizeWithReference(
            missingAreaRatio,
            options.MissingAreaReference);
        var radiusSeverity = MetricNormalization.NormalizeWithReference(
            radiusDeviation,
            options.RadiusDeviationReference);
        var roughnessSeverity = MetricNormalization.NormalizeWithReference(
            roughness,
            options.RoughnessReference);

        return MetricNormalization.Clamp01(
            (missingAreaSeverity * options.MissingAreaWeight)
            + (deviationSeverity * options.DeviationWeight)
            + (radiusSeverity * options.RadiusWeight)
            + (roughnessSeverity * options.RoughnessWeight));
    }

    public static double ComputeConditionScore(double severity, CornerGeometryAnalysisOptions options) =>
        Math.Clamp(100.0 - (MetricNormalization.Clamp01(severity) * options.ConditionPenaltyScale), 0.0, 100.0);

    public static double CombineWhiteningAndGeometry(double whiteningSeverity, double geometrySeverity) =>
        MetricNormalization.CombineComplement(whiteningSeverity, geometrySeverity);

    public static double ComputeCombinedConditionScore(double combinedSeverity, CornerGeometryAnalysisOptions options) =>
        Math.Clamp(100.0 - (MetricNormalization.Clamp01(combinedSeverity) * options.CombinedPenaltyScale), 0.0, 100.0);

    public static double ComputeFinalCornerScore(IReadOnlyList<double> cornerConditionScores, CornerGeometryAnalysisOptions options)
    {
        if (cornerConditionScores.Count == 0)
        {
            return 0;
        }

        var ordered = cornerConditionScores.OrderBy(s => s).ToArray();
        var worst = ordered[0];
        var secondWorst = ordered.Length > 1 ? ordered[1] : ordered[0];
        var average = ordered.Average();
        return (worst * options.WorstCornerWeight)
               + (secondWorst * options.SecondWorstCornerWeight)
               + (average * options.AverageCornerWeight);
    }

    public static CornerGeometryType Classify(
        double missingAreaSeverity,
        double radiusSeverity,
        double roughnessSeverity,
        CornerGeometryAnalysisOptions options)
    {
        if (missingAreaSeverity >= options.ChippingMissingAreaThreshold)
        {
            return missingAreaSeverity >= 0.7 ? CornerGeometryType.MissingMaterial : CornerGeometryType.Chipping;
        }

        if (radiusSeverity >= options.RoundingRadiusThreshold && missingAreaSeverity < 0.25)
        {
            return radiusSeverity >= 0.7 ? CornerGeometryType.Blunting : CornerGeometryType.Rounding;
        }

        if (roughnessSeverity >= options.RoughnessTypeThreshold)
        {
            return CornerGeometryType.RoughCut;
        }

        if (missingAreaSeverity < 0.12 && radiusSeverity < 0.18 && roughnessSeverity < 0.18)
        {
            return CornerGeometryType.Normal;
        }

        return CornerGeometryType.UnknownGeometryAnomaly;
    }
}
