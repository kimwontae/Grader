using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Preprocessing;
using OnePieceCardGrader.Imaging.Visualization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Surface;

public sealed class ScratchAnalyzer : IScratchAnalyzer
{
    private readonly ImageAnalysisOptions _options;
    private readonly AnalysisOptions _analysisOptions;
    private readonly SurfaceImageAligner _aligner = new();
    private readonly ScratchCandidateDetector _detector;
    private readonly ScratchMetricCalculator _metrics;
    private readonly ILogger<ScratchAnalyzer> _logger;

    public ScratchAnalyzer(
        ImageAnalysisOptions options,
        AnalysisOptions analysisOptions,
        ILogger<ScratchAnalyzer> logger)
    {
        _options = options;
        _analysisOptions = analysisOptions;
        _logger = logger;
        _detector = new ScratchCandidateDetector(options.Scratch);
        _metrics = new ScratchMetricCalculator(options.Scratch);
    }

    public ScratchAnalysisResult Analyze(
        SurfaceImageSet imageSet,
        CardSide side,
        MeasurementContext? context = null,
        IAnalysisDebugSink? debug = null)
    {
        var normalPath = side == CardSide.Front ? imageSet.NormalFrontPath : imageSet.NormalBackPath;
        var angledPath = side == CardSide.Front ? imageSet.AngledFrontPath : imageSet.AngledBackPath;
        if (string.IsNullOrWhiteSpace(normalPath) || !File.Exists(normalPath))
        {
            return new ScratchAnalysisResult
            {
                Severity = 0,
                ConditionScore = 100,
                Confidence = 0,
                Candidates = []
            };
        }

        using var normal = ExifOrientation.LoadOriented(normalPath);
        var work = Downscale(normal, _options.Scratch.DownscaleMaxEdge);
        try
        {
            debug?.Save("01_normal", work);
            using var glare = SurfaceGlareMaskBuilder.Build(work, _analysisOptions.Quality, _options.Scratch);
            AlignmentOutcome? alignment = null;
            Mat? angledAligned = null;
            Mat? lightingDiff = null;
            if (!string.IsNullOrWhiteSpace(angledPath) && File.Exists(angledPath))
            {
                using var angled = ExifOrientation.LoadOriented(angledPath);
                var angledWork = Downscale(angled, _options.Scratch.DownscaleMaxEdge);
                try
                {
                    debug?.Save("02_angled_original", angledWork);
                    alignment = _aligner.Align(work, angledWork, _options.Scratch);
                    if (alignment.Result.UsedAngledComparison && alignment.WarpedBgr is not null)
                    {
                        angledAligned = alignment.WarpedBgr.Clone();
                        debug?.Save("03_angled_aligned", angledAligned);
                        using var nLab = work.CvtColor(ColorConversionCodes.BGR2Lab);
                        using var aLab = angledAligned.CvtColor(ColorConversionCodes.BGR2Lab);
                        var nChannels = nLab.Split();
                        var aChannels = aLab.Split();
                        using var nL = nChannels[0];
                        using var aL = aChannels[0];
                        foreach (var ch in nChannels.Skip(1).Concat(aChannels.Skip(1)))
                        {
                            ch.Dispose();
                        }
                        lightingDiff = new Mat();
                        Cv2.Absdiff(nL, aL, lightingDiff);
                        debug?.Save("05_difference_map", lightingDiff);
                    }
                }
                finally
                {
                    if (!ReferenceEquals(angledWork, angled))
                    {
                        angledWork.Dispose();
                    }
                }
            }

            using var gray = work.CvtColor(ColorConversionCodes.BGR2GRAY);
            using var masks = _detector.Detect(gray, glare.Mask);
            debug?.Save("04_glare_mask", glare.Mask);
            debug?.Save("06_gradient_map", masks.GradientMap);
            debug?.Save("07_tophat", masks.TopHat);
            debug?.Save("08_blackhat", masks.BlackHat);
            debug?.Save("09_candidate_mask", masks.CandidateMask);

            var measured = _metrics.Measure(masks.CandidateMask, gray, masks.GradientMap, glare.Mask, lightingDiff, work.Size());
            using var filteredVis = ScratchOverlayRenderer.RenderCandidates(work, measured);
            debug?.Save("10_filtered_candidates", filteredVis);
            using var overlay = ScratchOverlayRenderer.Render(work, measured, glare.Mask);
            debug?.Save("11_final_overlay", overlay);

            var candidates = measured
                .OrderByDescending(m => m.Severity)
                .Select((m, index) => new ScratchCandidate
                {
                    Id = index + 1,
                    BoundingBox = NormalizedRect.FromPixels(
                        m.BoundingBox.X, m.BoundingBox.Y, m.BoundingBox.Width, m.BoundingBox.Height, work.Width, work.Height),
                    LengthRatio = m.LengthRatio,
                    WidthRatio = m.WidthRatio,
                    AspectRatio = m.AspectRatio,
                    LocalContrast = m.LocalContrast,
                    GradientStrength = m.GradientStrength,
                    Straightness = m.Straightness,
                    Curvature = m.Curvature,
                    LightingResponse = m.LightingResponse,
                    GlareOverlap = m.GlareOverlap,
                    WidthConsistency = m.WidthConsistency,
                    DetectionConfidence = m.DetectionConfidence,
                    Severity = m.Severity,
                    Type = Classify(m)
                })
                .ToArray();

            var worst = candidates.FirstOrDefault()?.Severity ?? 0;
            var second = candidates.ElementAtOrDefault(1)?.Severity ?? 0;
            var affected = measured.Sum(m => m.AreaRatio);
            var affectedSeverity = MetricNormalization.NormalizeWithReference(affected, _options.Scratch.SevereAffectedAreaRatio);
            var countSeverity = MetricNormalization.NormalizeWithReference(candidates.Length, _options.Scratch.SevereCount);
            var surfaceSeverity = ScratchScoreCalculator.ComputeSurfaceSeverity(
                worst, second, affectedSeverity, countSeverity, _options.Scratch);
            var condition = ScratchScoreCalculator.ComputeConditionScore(surfaceSeverity, _options.Scratch);

            var lightingAvailability = alignment?.Result.UsedAngledComparison == true ? 1.0 : 0.35;
            var shapeConfidence = candidates.Length == 0
                ? 0.7
                : candidates.Average(c => c.DetectionConfidence);
            var confidence = ScratchScoreCalculator.ComputeConfidence(
                context?.ImageQuality ?? 70,
                alignment is null ? 40 : 70,
                alignment?.Result.Confidence ?? 0.2,
                glare.CoverageRatio,
                shapeConfidence,
                lightingAvailability,
                _options.Scratch);

            _logger.LogInformation(
                "[Scratch] Side={Side} Candidates={Count} WorstSeverity={Worst:F2} AlignmentConfidence={Align:F2} SurfaceSeverity={Severity:F3}",
                side,
                candidates.Length,
                worst,
                alignment?.Result.Confidence ?? 0,
                surfaceSeverity);

            lightingDiff?.Dispose();
            angledAligned?.Dispose();
            alignment?.Dispose();

            return new ScratchAnalysisResult
            {
                Severity = surfaceSeverity,
                ConditionScore = condition,
                Confidence = confidence,
                CandidateCount = candidates.Length,
                WorstScratchSeverity = worst,
                TotalAffectedAreaRatio = affected,
                Candidates = candidates,
                Alignment = alignment?.Result
            };
        }
        finally
        {
            if (!ReferenceEquals(work, normal))
            {
                work.Dispose();
            }
        }
    }

    private static ScratchCandidateType Classify(ScratchComponentMetrics metrics)
    {
        if (metrics.LightingResponse < 0.18 && metrics.WidthConsistency > 0.7 && metrics.Straightness > 0.9)
        {
            return ScratchCandidateType.PrintLineCandidate;
        }

        if (metrics.DetectionConfidence >= 0.55 && metrics.LightingResponse >= 0.2)
        {
            return ScratchCandidateType.ScratchCandidate;
        }

        return ScratchCandidateType.LinearSurfaceAnomaly;
    }

    private static Mat Downscale(Mat source, int maxEdge)
    {
        var scale = Math.Min(1.0, maxEdge / (double)Math.Max(source.Width, source.Height));
        return scale >= 0.999 ? source : source.Resize(new Size(), scale, scale, InterpolationFlags.Area);
    }
}
