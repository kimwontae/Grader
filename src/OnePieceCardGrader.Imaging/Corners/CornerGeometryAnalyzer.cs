using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Detection;
using OnePieceCardGrader.Imaging.Visualization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Corners;

public sealed class CornerGeometryAnalyzer : ICornerGeometryAnalyzer
{
    private readonly ImageAnalysisOptions _options;
    private readonly CornerContourDetector _contourDetector = new();
    private readonly CornerGeometryMetricCalculator _metrics = new();
    private readonly ILogger<CornerGeometryAnalyzer> _logger;

    public CornerGeometryAnalyzer(ImageAnalysisOptions options, ILogger<CornerGeometryAnalyzer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public CornerGeometryResult Analyze(
        Mat normalizedCard,
        CornerPosition position,
        Mat? cardMask = null,
        MeasurementContext? context = null,
        IAnalysisDebugSink? debug = null)
    {
        ArgumentNullException.ThrowIfNull(normalizedCard);
        var cfg = _options.CornerGeometry;
        var ownsMask = cardMask is null;
        var mask = cardMask ?? CardMaskExtractor.Extract(normalizedCard, cfg.CardMaskThreshold);
        try
        {
            var roi = ExtractRoi(normalizedCard.Size(), position, cfg.CornerRoiRatio);
            using var roiBgr = new Mat(normalizedCard, roi);
            using var roiMask = new Mat(mask, roi);
            var contour = _contourDetector.Extract(roiMask, position, cfg);
            using var fit = _metrics.Fit(roiMask, contour, position, normalizedCard.Width, cfg);

            var severity = CornerGeometryScoreCalculator.ComputeSeverity(
                fit.MeanDeviation,
                fit.MissingAreaRatio,
                fit.RadiusDeviation,
                fit.CurvatureStdDev,
                cfg);
            var missingSeverity = MetricNormalization.NormalizeWithReference(fit.MissingAreaRatio, cfg.MissingAreaReference);
            var radiusSeverity = MetricNormalization.NormalizeWithReference(fit.RadiusDeviation, cfg.RadiusDeviationReference);
            var roughnessSeverity = MetricNormalization.NormalizeWithReference(fit.CurvatureStdDev, cfg.RoughnessReference);
            var type = CornerGeometryScoreCalculator.Classify(missingSeverity, radiusSeverity, roughnessSeverity, cfg);
            var score = CornerGeometryScoreCalculator.ComputeConditionScore(severity, cfg);
            var focus = (context?.Focus ?? 70) / 100.0;
            var quality = (context?.ImageQuality ?? 70) / 100.0;
            var confidence = Math.Clamp(((quality * 0.45) + (focus * 0.25) + (fit.Continuity * 0.30)) * 100.0, 0, 100);

            if (debug is { Enabled: true })
            {
                debug.Save("01_corner_roi", roiBgr);
                debug.Save("02_card_mask", roiMask);
                using var contourVis = roiBgr.Clone();
                if (contour.Original.Length > 0)
                {
                    Cv2.Polylines(contourVis, new[] { contour.Original }, false, new Scalar(40, 220, 255), 2);
                }

                debug.Save("03_actual_contour", contourVis);
                using var lineVis = CornerOverlayRenderer.DrawFittedLines(roiBgr, fit);
                debug.Save("04_fitted_lines", lineVis);
                using var arcVis = CornerOverlayRenderer.DrawIdealArc(roiBgr, fit, position);
                debug.Save("05_ideal_arc", arcVis);
                using var missingVis = CornerOverlayRenderer.DrawMissing(roiBgr, fit);
                debug.Save("06_missing_area", missingVis);
                using var overlay = CornerOverlayRenderer.Render(normalizedCard, roi, contour, fit, position);
                debug.Save("07_geometry_overlay", overlay);
            }

            _logger.LogInformation(
                "[Corner] Position={Position} MissingArea={Missing:F4} Deviation={Deviation:F4} RadiusDeviation={Radius:F4} Roughness={Rough:F4} GeometrySeverity={Severity:F3}",
                position,
                fit.MissingAreaRatio,
                fit.MeanDeviation,
                fit.RadiusDeviation,
                fit.CurvatureStdDev,
                severity);

            return new CornerGeometryResult
            {
                Position = position,
                Severity = severity,
                ConditionScore = score,
                Confidence = confidence,
                MeanContourDeviation = fit.MeanDeviation,
                MaxContourDeviation = fit.MaxDeviation,
                MissingAreaRatio = fit.MissingAreaRatio,
                ExcessAreaRatio = fit.ExcessAreaRatio,
                RadiusDeviation = fit.RadiusDeviation,
                MeasuredRadiusRatio = fit.MeasuredRadiusRatio,
                ExpectedRadiusRatio = fit.ExpectedRadiusRatio,
                Roughness = fit.CurvatureStdDev,
                CurvatureMean = fit.CurvatureMean,
                CurvatureStdDev = fit.CurvatureStdDev,
                HighFrequencyCurvatureEnergy = fit.HighFrequencyEnergy,
                Continuity = fit.Continuity,
                Type = type
            };
        }
        finally
        {
            if (ownsMask)
            {
                mask.Dispose();
            }
        }
    }

    internal static Rect ExtractRoi(Size size, CornerPosition position, double ratio)
    {
        var w = Math.Max(24, (int)Math.Round(size.Width * ratio));
        var h = Math.Max(24, (int)Math.Round(size.Height * ratio));
        return position switch
        {
            CornerPosition.TopLeft => new Rect(0, 0, w, h),
            CornerPosition.TopRight => new Rect(size.Width - w, 0, w, h),
            CornerPosition.BottomRight => new Rect(size.Width - w, size.Height - h, w, h),
            _ => new Rect(0, size.Height - h, w, h)
        };
    }
}
