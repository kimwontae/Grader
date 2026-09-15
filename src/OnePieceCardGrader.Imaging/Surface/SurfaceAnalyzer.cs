using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using Microsoft.Extensions.Logging;

namespace OnePieceCardGrader.Imaging.Surface;

public sealed class SurfaceAnalyzer : ISurfaceAnalyzer
{
    private readonly IScratchAnalyzer _scratchAnalyzer;
    private readonly ILogger<SurfaceAnalyzer> _logger;

    public SurfaceAnalyzer(IScratchAnalyzer scratchAnalyzer, ILogger<SurfaceAnalyzer> logger)
    {
        _scratchAnalyzer = scratchAnalyzer;
        _logger = logger;
    }

    public SurfaceAnalysisResult Analyze(SurfaceImageSet images)
    {
        if (images.NormalFrontPath is null && images.NormalBackPath is null)
        {
            return new SurfaceAnalysisResult
            {
                Status = AnalysisStatus.Skipped,
                UnavailableReason = "Surface 분석용 이미지가 없습니다.",
                Confidence = 0
            };
        }

        var front = string.IsNullOrWhiteSpace(images.NormalFrontPath)
            ? null
            : _scratchAnalyzer.Analyze(images, CardSide.Front);
        var back = string.IsNullOrWhiteSpace(images.NormalBackPath)
            ? null
            : _scratchAnalyzer.Analyze(images, CardSide.Back);

        var defects = new List<DetectedDefect>();
        if (front is not null)
        {
            defects.AddRange(front.Candidates.Select(c => DefectFactory.FromScratch(c, CardSide.Front)));
        }

        if (back is not null)
        {
            defects.AddRange(back.Candidates.Select(c => DefectFactory.FromScratch(c, CardSide.Back)));
        }

        var frontScore = front?.ConditionScore ?? 100;
        var backScore = back?.ConditionScore ?? 100;
        var usedAngled = (front?.Alignment?.UsedAngledComparison ?? false)
                         || (back?.Alignment?.UsedAngledComparison ?? false);
        var confidence = Math.Max(front?.Confidence ?? 0, back?.Confidence ?? 0) / 100.0
                         * (images.UsedNormalizedFallback ? 0.8 : 1.0);

        _logger.LogInformation(
            "Surface analysis score front={Front:F1} back={Back:F1} defects={Count}",
            frontScore,
            backScore,
            defects.Count);

        return new SurfaceAnalysisResult
        {
            Status = AnalysisStatus.Completed,
            FrontScore = frontScore,
            BackScore = backScore,
            ConditionScore = Math.Min(frontScore, backScore),
            GradeCap = DefectScoreMath.GradeCap(defects),
            Confidence = Math.Clamp(confidence, 0.15, 0.95),
            Defects = defects,
            UsedAngledLight = usedAngled,
            UsedMacroFallback = images.UsedNormalizedFallback,
            FrontScratch = front,
            BackScratch = back
        };
    }
}
