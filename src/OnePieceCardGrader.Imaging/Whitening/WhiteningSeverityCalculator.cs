using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Imaging.Whitening;

public sealed class WhiteningSeverityCalculator
{
    public WhiteningAnalysisResult Build(
        WhiteningMetricSet metrics,
        double glareOverlapRatio,
        double referenceLStdDev,
        MeasurementContext? context,
        WhiteningAnalysisOptions options)
    {
        var severity = WhiteningScoreCalculator.ComputeSeverity(
            metrics.LengthRatio,
            metrics.AreaRatio,
            metrics.MeanDeltaE,
            metrics.LargestDefectRatio,
            options);
        var largestSeverity = MetricNormalization.NormalizeWithReference(
            metrics.LargestDefectRatio,
            options.SevereLargestDefectRatio);
        var score = WhiteningScoreCalculator.ComputeConditionScore(severity, largestSeverity, options);

        var referenceStability = MetricNormalization.Clamp01(1.0 - (referenceLStdDev / 28.0));
        var glareQuality = MetricNormalization.Clamp01(1.0 - glareOverlapRatio);
        var imageQuality = context?.ImageQuality ?? 70;
        var detection = context?.CardDetectionConfidence ?? 0.8;
        var confidence = WhiteningScoreCalculator.ComputeConfidence(
            imageQuality,
            referenceStability,
            glareQuality,
            detection,
            options);

        return new WhiteningAnalysisResult
        {
            Severity = severity,
            ConditionScore = score,
            Confidence = confidence,
            WhiteningLengthRatio = metrics.LengthRatio,
            WhiteningAreaRatio = metrics.AreaRatio,
            LargestDefectRatio = metrics.LargestDefectRatio,
            MeanDeltaE = metrics.MeanDeltaE,
            MaxDeltaE = metrics.MaxDeltaE,
            MeanBrightnessIncrease = metrics.MeanBrightnessIncrease,
            DefectCount = metrics.Components.Count,
            GlareOverlapRatio = glareOverlapRatio,
            ReferenceColorVariance = referenceLStdDev,
            Components = metrics.Components
        };
    }
}
