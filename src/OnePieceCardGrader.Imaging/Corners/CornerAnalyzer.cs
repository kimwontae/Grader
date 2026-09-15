using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Detection;
using OnePieceCardGrader.Imaging.Quality;
using OnePieceCardGrader.Imaging.Whitening;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Corners;

public sealed class CornerAnalyzer : ICornerAnalyzer
{
    private readonly ImageAnalysisOptions _options;
    private readonly AnalysisOptions _analysisOptions;
    private readonly IWhiteningAnalyzer _whiteningAnalyzer;
    private readonly ICornerGeometryAnalyzer _geometryAnalyzer;
    private readonly ILogger<CornerAnalyzer> _logger;

    public CornerAnalyzer(
        ImageAnalysisOptions options,
        AnalysisOptions analysisOptions,
        IWhiteningAnalyzer whiteningAnalyzer,
        ICornerGeometryAnalyzer geometryAnalyzer,
        ILogger<CornerAnalyzer> logger)
    {
        _options = options;
        _analysisOptions = analysisOptions;
        _whiteningAnalyzer = whiteningAnalyzer;
        _geometryAnalyzer = geometryAnalyzer;
        _logger = logger;
    }

    public CornerResult Analyze(
        OpenCvImage image,
        CornerPosition position,
        CardSide side,
        bool isMacroImage,
        NormalizedRect? sourceRegion = null,
        QuadCorners? macroCardCorners = null)
    {
        var src = MatAdapter.Unwrap(image);
        using var expanded = isMacroImage
            ? ExpandMacro(src, position, _analysisOptions.Normalization, macroCardCorners)
            : null;
        var card = expanded ?? src;
        var glareDet = GlareDetector.Analyze(card, _analysisOptions.Quality);
        using var glare = new GlareMask(
            glareDet.Mask ?? new Mat(card.Size(), MatType.CV_8UC1, Scalar.All(0)),
            glareDet.CoverageRatio);
        using var cardMask = CardMaskExtractor.Extract(card, _options.CornerGeometry.CardMaskThreshold);
        var context = new MeasurementContext
        {
            ImageQuality = isMacroImage ? 78 : 70,
            Focus = 70,
            CardDetectionConfidence = macroCardCorners is not null ? 0.97 : isMacroImage ? 0.9 : 0.75
        };

        var whitening = _whiteningAnalyzer.Analyze(
            card,
            WhiteningRegionMap.FromCorner(position),
            glare,
            context);
        var geometry = _geometryAnalyzer.Analyze(card, position, cardMask, context);
        var combinedSeverity = CornerGeometryScoreCalculator.CombineWhiteningAndGeometry(whitening.Severity, geometry.Severity);
        var combinedScore = CornerGeometryScoreCalculator.ComputeCombinedConditionScore(combinedSeverity, _options.CornerGeometry);
        var defects = new List<DetectedDefect>();
        var whiteningDefect = DefectFactory.FromWhitening(
            whitening,
            side,
            position.ToString(),
            DefectType.CornerWhitening,
            sourceRegion);
        if (whiteningDefect is not null)
        {
            defects.Add(whiteningDefect);
        }

        var geometryDefect = DefectFactory.FromGeometry(geometry, side, sourceRegion);
        if (geometryDefect is not null)
        {
            defects.Add(geometryDefect);
        }

        var severity = DefectFactory.FromSeverity(combinedSeverity);
        _logger.LogInformation(
            "Corner {Side} {Position} whitening={W:F3} geometry={G:F3} combined={C:F1} macro={Macro}",
            side,
            position,
            whitening.Severity,
            geometry.Severity,
            combinedScore,
            isMacroImage);

        return new CornerResult
        {
            Position = position,
            Side = side,
            SharpnessScore = context.Focus,
            WhiteningScore = whitening.ConditionScore,
            GeometryScore = geometry.ConditionScore,
            CombinedScore = combinedScore,
            Severity = severity,
            Confidence = Math.Clamp(((whitening.Confidence + geometry.Confidence) / 2.0) / 100.0, 0, 1),
            Defects = defects,
            Status = AnalysisStatus.Completed,
            Region = sourceRegion,
            UsedMacroImage = isMacroImage,
            UsedManualRegion = macroCardCorners is not null,
            WhiteningSeverity = whitening.Severity,
            GeometrySeverity = geometry.Severity,
            Whitening = whitening,
            Geometry = geometry
        };
    }

    private static Mat ExpandMacro(Mat macro, CornerPosition position, NormalizationOptions normalization, QuadCorners? corners)
    {
        Mat? warped = null;
        try
        {
            if (corners is not null)
            {
                var previewRoi = CornerGeometryAnalyzer.ExtractRoi(
                    new Size(normalization.CanonicalWidth, normalization.CanonicalHeight),
                    position,
                    0.22);
                warped = WarpQuad(macro, corners, previewRoi.Size);
            }

            var fillSource = warped ?? macro;
            var canvas = new Mat(
                normalization.CanonicalHeight,
                normalization.CanonicalWidth,
                MatType.CV_8UC3,
                Cv2.Mean(fillSource));
            var roi = CornerGeometryAnalyzer.ExtractRoi(canvas.Size(), position, 0.22);
            using var dest = new Mat(canvas, roi);
            if (warped is not null)
            {
                using var resized = warped.Size() == roi.Size ? warped.Clone() : warped.Resize(roi.Size);
                resized.CopyTo(dest);
            }
            else
            {
                using var resized = macro.Resize(roi.Size);
                resized.CopyTo(dest);
            }

            return canvas;
        }
        finally
        {
            warped?.Dispose();
        }
    }

    private static Mat WarpQuad(Mat source, QuadCorners corners, Size destSize)
    {
        var srcPoints = InputArray.Create(new[]
        {
            new Point2f((float)corners.TopLeft.X, (float)corners.TopLeft.Y),
            new Point2f((float)corners.TopRight.X, (float)corners.TopRight.Y),
            new Point2f((float)corners.BottomRight.X, (float)corners.BottomRight.Y),
            new Point2f((float)corners.BottomLeft.X, (float)corners.BottomLeft.Y)
        });
        var destPoints = InputArray.Create(new[]
        {
            new Point2f(0, 0),
            new Point2f(destSize.Width - 1, 0),
            new Point2f(destSize.Width - 1, destSize.Height - 1),
            new Point2f(0, destSize.Height - 1)
        });
        using var matrix = Cv2.GetPerspectiveTransform(srcPoints, destPoints);
        var warped = new Mat();
        Cv2.WarpPerspective(source, warped, matrix, destSize, InterpolationFlags.Cubic);
        return warped;
    }
}
