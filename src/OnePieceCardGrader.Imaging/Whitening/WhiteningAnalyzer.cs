using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Detection;
using OnePieceCardGrader.Imaging.Visualization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Whitening;

public sealed class WhiteningAnalyzer : IWhiteningAnalyzer
{
    private readonly ImageAnalysisOptions _options;
    private readonly WhiteningCandidateDetector _detector;
    private readonly WhiteningMetricCalculator _metrics = new();
    private readonly WhiteningSeverityCalculator _severity = new();
    private readonly ILogger<WhiteningAnalyzer> _logger;

    public WhiteningAnalyzer(ImageAnalysisOptions options, ILogger<WhiteningAnalyzer> logger)
    {
        _options = options;
        _logger = logger;
        _detector = new WhiteningCandidateDetector(options.Whitening);
    }

    public WhiteningAnalysisResult Analyze(
        Mat normalizedCard,
        WhiteningRegion region,
        GlareMask? glareMask = null,
        MeasurementContext? context = null,
        IAnalysisDebugSink? debug = null)
    {
        ArgumentNullException.ThrowIfNull(normalizedCard);
        var cfg = _options.Whitening;
        using var cardMask = CardMaskExtractor.Extract(normalizedCard, 8);
        var roiInfo = WhiteningRoi.FromCard(normalizedCard.Width, normalizedCard.Height, region, cfg);
        var roi = ClampRoi(roiInfo.Rect, normalizedCard.Size());
        using var roiBgr = new Mat(normalizedCard, roi);
        using var roiMask = new Mat(cardMask, roi);

        using var lab = roiBgr.CvtColor(ColorConversionCodes.BGR2Lab);
        using var hsv = roiBgr.CvtColor(ColorConversionCodes.BGR2HSV);
        var labChannels = lab.Split();
        var hsvChannels = hsv.Split();
        using var l8 = labChannels[0];
        using var a8 = labChannels[1];
        using var b8 = labChannels[2];
        using var s8 = hsvChannels[1];
        DisposeUnused(labChannels, l8, a8, b8);
        DisposeUnused(hsvChannels, s8);

        using var labL = new Mat();
        using var labA = new Mat();
        using var labB = new Mat();
        using var hsvS = new Mat();
        l8.ConvertTo(labL, MatType.CV_32F, 100.0 / 255.0);
        a8.ConvertTo(labA, MatType.CV_32F, 1.0, -128.0);
        b8.ConvertTo(labB, MatType.CV_32F, 1.0, -128.0);
        s8.ConvertTo(hsvS, MatType.CV_32F);

        var offset = Math.Clamp((cfg.LocalReferenceMinPx + cfg.LocalReferenceMaxPx) / 2, cfg.LocalReferenceMinPx, cfg.LocalReferenceMaxPx);
        var referenceStrip = Math.Max(3, (int)Math.Round(
            (region is WhiteningRegion.LeftEdge or WhiteningRegion.RightEdge ? normalizedCard.Width : normalizedCard.Height)
            * cfg.ReferenceInnerOffsetRatio));
        var (mapX, mapY) = WhiteningRoi.BuildInwardMaps(roi.Size, roiInfo.Inward, offset, referenceStrip, region);
        using (mapX)
        using (mapY)
        {
            using var candidateZone = WhiteningRoi.BuildCandidateZone(roi.Size, region, referenceStrip);
            using var referenceZone = WhiteningRoi.BuildReferenceZone(roi.Size, region, referenceStrip);
            Cv2.BitwiseAnd(candidateZone, roiMask, candidateZone);
            Cv2.BitwiseAnd(referenceZone, roiMask, referenceZone);

            using var glareRoiRaw = CropGlare(glareMask, roi);
            using var boundary = WhiteningRoi.BuildOuterBoundaryMask(normalizedCard.Size(), region);
            using var glareRoi = ExcludeBoundaryFromGlare(glareRoiRaw, boundary, roi);
            using var detected = _detector.Detect(
                labL, labA, labB, hsvS, candidateZone, roiMask, mapX, mapY, referenceZone, glareRoi);

            using var inverted = new Mat();
            Cv2.BitwiseNot(boundary, inverted);
            using var distFull = new Mat();
            Cv2.DistanceTransform(inverted, distFull, DistanceTypes.L2, DistanceTransformMasks.Mask3);
            using var distRoi = new Mat(distFull, roi);

            var roiCardPixels = Math.Max(1, Cv2.CountNonZero(roiMask));
            var metrics = _metrics.Compute(
                detected.CandidateMask,
                detected.DeltaE,
                detected.BrightnessIncrease,
                detected.PixelScore,
                distRoi,
                region,
                normalizedCard.Size(),
                    roiCardPixels,
                    cfg,
                    roi);

            var result = _severity.Build(metrics, detected.GlareOverlapRatio, detected.ReferenceLStdDev, context, cfg);

            debug?.Save("01_edge_roi", roiBgr);
            if (debug is { Enabled: true })
            {
                using var refVis = ColorizeMask(roiBgr, candidateZone, referenceZone);
                debug.Save("02_reference_region", refVis);
                using var deltaVis = VisualizeFloat(detected.DeltaE);
                debug.Save("03_deltae_map", deltaVis);
                using var candVis = OverlayMask(roiBgr, detected.CandidateMask, new Scalar(0, 220, 255));
                debug.Save("04_whitening_candidate", candVis);
                debug.Save("05_glare_removed", OverlayMask(roiBgr, detected.CandidateMask, new Scalar(40, 180, 255)));
                using var compVis = WhiteningOverlayRenderer.RenderComponents(roiBgr, result.Components, roi, normalizedCard.Size());
                debug.Save("06_components", compVis);
                using var final = WhiteningOverlayRenderer.Render(
                    normalizedCard, roi, candidateZone, referenceZone, detected.CandidateMask, result.Components, region);
                debug.Save("07_final_overlay", final);
            }

            _logger.LogInformation(
                "[Whitening] Region={Region} Components={Count} LengthRatio={Length:F4} AreaRatio={Area:F4} MeanDeltaE={Delta:F1} Severity={Severity:F3} Confidence={Confidence:F1}",
                region,
                result.DefectCount,
                result.WhiteningLengthRatio,
                result.WhiteningAreaRatio,
                result.MeanDeltaE,
                result.Severity,
                result.Confidence);

            return result;
        }
    }

    private static Rect ClampRoi(Rect roi, Size size)
    {
        var x = Math.Clamp(roi.X, 0, Math.Max(0, size.Width - 1));
        var y = Math.Clamp(roi.Y, 0, Math.Max(0, size.Height - 1));
        var w = Math.Clamp(roi.Width, 1, size.Width - x);
        var h = Math.Clamp(roi.Height, 1, size.Height - y);
        return new Rect(x, y, w, h);
    }

    private static Mat? ExcludeBoundaryFromGlare(Mat? glareRoi, Mat fullBoundary, Rect roi)
    {
        if (glareRoi is null)
        {
            return null;
        }

        using var boundaryRoi = new Mat(fullBoundary, roi);
        using var dilated = new Mat();
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(9, 9));
        Cv2.Dilate(boundaryRoi, dilated, kernel);
        using var keep = new Mat();
        Cv2.BitwiseNot(dilated, keep);
        var filtered = new Mat();
        Cv2.BitwiseAnd(glareRoi, keep, filtered);
        return filtered;
    }

    private static Mat? CropGlare(GlareMask? glare, Rect roi)
    {
        if (glare?.Mask is null || glare.Mask.Empty())
        {
            return null;
        }

        if (glare.Mask.Width < roi.X + roi.Width || glare.Mask.Height < roi.Y + roi.Height)
        {
            return null;
        }

        return new Mat(glare.Mask, roi).Clone();
    }

    private static Mat ColorizeMask(Mat bgr, Mat candidate, Mat reference)
    {
        var vis = bgr.Clone();
        vis.SetTo(new Scalar(40, 180, 80), reference);
        vis.SetTo(new Scalar(40, 220, 255), candidate);
        return vis;
    }

    private static Mat OverlayMask(Mat bgr, Mat mask, Scalar color)
    {
        var vis = bgr.Clone();
        vis.SetTo(color, mask);
        return vis;
    }

    private static Mat VisualizeFloat(Mat values)
    {
        using var normalized = new Mat();
        Cv2.Normalize(values, normalized, 0, 255, NormTypes.MinMax);
        var vis = new Mat();
        normalized.ConvertTo(vis, MatType.CV_8U);
        Cv2.ApplyColorMap(vis, vis, ColormapTypes.Turbo);
        return vis;
    }

    private static void DisposeUnused(Mat[] channels, params Mat[] keep)
    {
        foreach (var channel in channels)
        {
            if (keep.All(k => !ReferenceEquals(k, channel)))
            {
                channel.Dispose();
            }
        }
    }
}
