using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Explanations;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using Microsoft.Extensions.Logging;

namespace OnePieceCardGrader.Grading.Services;

public sealed class GradingEngine : IGradingEngine
{
    private readonly IGradingProfileProvider _profiles;
    private readonly ILogger<GradingEngine> _logger;

    public GradingEngine(IGradingProfileProvider profiles, ILogger<GradingEngine> logger)
    {
        _profiles = profiles;
        _logger = logger;
    }

    public GradingResult Calculate(CardAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        var profile = _profiles.GetProfile(AppConstants.DefaultProfileName);

        var (centeringScore, centeringCap) = ResolveCentering(analysis, profile);
        var centeringGrade = new CategoryGrade
        {
            Name = "Centering",
            Status = analysis.Centering.Status,
            ConditionScore = centeringScore,
            GradeCap = centeringCap,
            Confidence = analysis.Centering.Front?.Confidence ?? 0,
            Notes = analysis.Centering.Notes
        };

        var cornerDefects = analysis.Corners.Corners.SelectMany(c => c.Defects).ToArray();
        var (cornerScore, resolvedCornerCap) = ResolveCategory(
            analysis.Corners.Status,
            analysis.Corners.ConditionScore,
            analysis.Corners.GradeCap,
            cornerDefects);
        var cornerGrade = ToCategory(
            "Corners",
            analysis.Corners.Status,
            cornerScore,
            resolvedCornerCap,
            analysis.Corners.Corners.Count == 0 ? 0 : analysis.Corners.Corners.Average(c => c.Confidence),
            analysis.Corners.UnavailableReason,
            ActiveNotes(cornerDefects));

        var (surfaceScore, resolvedSurfaceCap) = ResolveCategory(
            analysis.Surface.Status,
            analysis.Surface.ConditionScore,
            analysis.Surface.GradeCap,
            analysis.Surface.Defects);
        var surfaceGrade = ToCategory(
            "Surface",
            analysis.Surface.Status,
            surfaceScore,
            resolvedSurfaceCap,
            analysis.Surface.Confidence,
            analysis.Surface.UnavailableReason,
            ActiveNotes(analysis.Surface.Defects));

        var (edgeScore, resolvedEdgeCap) = ResolveCategory(
            analysis.Edges.Status,
            analysis.Edges.ConditionScore,
            analysis.Edges.GradeCap,
            analysis.Edges.Defects);
        var edgeGrade = ToCategory(
            "Edges",
            analysis.Edges.Status,
            edgeScore,
            resolvedEdgeCap,
            analysis.Edges.Confidence > 1 ? analysis.Edges.Confidence / 100.0 : analysis.Edges.Confidence,
            analysis.Edges.UnavailableReason,
            ActiveNotes(analysis.Edges.Defects));

        var implementedScores = new List<(double Score, double Weight)>();
        if (analysis.Centering.Status == AnalysisStatus.Completed)
        {
            implementedScores.Add((centeringScore, profile.Weights.Centering));
        }

        if (analysis.Corners.Status == AnalysisStatus.Completed)
        {
            implementedScores.Add((cornerGrade.ConditionScore, profile.Weights.Corners));
        }

        if (analysis.Surface.Status == AnalysisStatus.Completed)
        {
            implementedScores.Add((surfaceGrade.ConditionScore, profile.Weights.Surface));
        }

        if (analysis.Edges.Status == AnalysisStatus.Completed)
        {
            implementedScores.Add((edgeGrade.ConditionScore, profile.Weights.Edges));
        }

        var weighted = implementedScores.Count == 0
            ? 0
            : implementedScores.Sum(x => x.Score * x.Weight) / implementedScores.Sum(x => x.Weight);
        var compositeGrade = ScoreToGrade(weighted, profile);
        var predicted = Math.Min(compositeGrade, centeringCap);
        if (cornerGrade.GradeCap is int cornerCap)
        {
            predicted = Math.Min(predicted, cornerCap);
        }

        if (surfaceGrade.GradeCap is int surfaceCap)
        {
            predicted = Math.Min(predicted, surfaceCap);
        }

        if (edgeGrade.GradeCap is int edgeCap)
        {
            predicted = Math.Min(predicted, edgeCap);
        }

        var unimplemented = analysis.Corners.Status != AnalysisStatus.Completed
                            || analysis.Surface.Status != AnalysisStatus.Completed
                            || analysis.Edges.Status != AnalysisStatus.Completed;
        var rangeMin = Math.Max(AppConstants.MinGrade, predicted - (unimplemented ? 1 : 0));
        var rangeMax = predicted;
        var borderline = IsBorderline(analysis.Centering.Front, analysis.Centering.Back, profile)
                         || Math.Abs(weighted - ThresholdFor(predicted, profile)) <= 2;
        if (borderline)
        {
            rangeMax = Math.Min(AppConstants.MaxGrade, predicted + 1);
            rangeMin = Math.Max(AppConstants.MinGrade, predicted - 1);
        }

        var confidence = ComputeConfidence(analysis);
        var reasons = new List<string>();
        if (centeringCap < 10)
        {
            var centeringNote = analysis.Defects
                .FirstOrDefault(d => d.Type == DefectType.CenteringOff && DefectNarrator.IsActive(d));
            reasons.Add(centeringNote is null
                ? $"센터링 오차 때문에 예상 상한이 PSA {centeringCap}입니다."
                : $"{centeringNote.Title}. {centeringNote.Impact}");
        }

        foreach (var note in cornerGrade.Notes.Take(3))
        {
            reasons.Add(note);
        }

        foreach (var note in surfaceGrade.Notes.Take(3))
        {
            reasons.Add(note);
        }

        foreach (var note in edgeGrade.Notes.Take(3))
        {
            reasons.Add(note);
        }

        if (analysis.Corners.Status != AnalysisStatus.Completed)
        {
            reasons.Add("Corner 확대 사진이 없으면 전체 사진 ROI로 분석하며 신뢰도가 낮아집니다.");
        }

        _logger.LogInformation(
            "Grading engine predicted {Grade} (range {Min}-{Max}, confidence {Confidence:F0})",
            predicted,
            rangeMin,
            rangeMax,
            confidence);

        var tenLimiter = analysis.Defects
                             .Where(d => DefectNarrator.IsActive(d) && (d.GradeCap ?? 10) < 10)
                             .OrderBy(d => d.GradeCap ?? 10)
                             .ThenByDescending(d => d.Severity)
                             .Select(FormatDefect)
                             .FirstOrDefault()
                         ?? (centeringCap < 10
                             ? "센터링이 PSA 10 공개 기준 범위를 벗어났습니다."
                             : null);

        return new GradingResult
        {
            PredictedGrade = predicted,
            GradeRangeMin = rangeMin,
            GradeRangeMax = Math.Max(rangeMax, predicted),
            AnalysisConfidence = confidence,
            Centering = centeringGrade,
            Corners = cornerGrade,
            Edges = edgeGrade,
            Surface = surfaceGrade,
            Defects = analysis.Defects,
            GradeLimitReasons = reasons.Distinct().ToArray(),
            Summary = $"예상 PSA {predicted} (범위 {rangeMin}~{Math.Max(rangeMax, predicted)})",
            IsBorderline = borderline,
            PrimaryTenLimiter = tenLimiter
        };
    }

    public static GradingResult ApplyCriticalCap(GradingResult grading, GradeCapResult critical) =>
        new()
        {
            PredictedGrade = Math.Min(grading.PredictedGrade, critical.MaxGrade),
            GradeRangeMin = Math.Min(grading.GradeRangeMin, critical.MaxGrade),
            GradeRangeMax = Math.Min(grading.GradeRangeMax, critical.MaxGrade),
            AnalysisConfidence = grading.AnalysisConfidence,
            Centering = grading.Centering,
            Corners = grading.Corners,
            Edges = grading.Edges,
            Surface = grading.Surface,
            Defects = grading.Defects,
            GradeLimitReasons = grading.GradeLimitReasons.Concat(critical.Reasons).Distinct().ToArray(),
            Summary = grading.Summary,
            IsBorderline = grading.IsBorderline,
            PrimaryTenLimiter = grading.PrimaryTenLimiter ?? critical.Reasons.FirstOrDefault()
        };

    private static (double Score, int Cap) ResolveCentering(CardAnalysisResult analysis, GradingProfile profile)
    {
        var centeringDefects = analysis.Defects.Where(d => d.Type == DefectType.CenteringOff).ToArray();
        var front = analysis.Centering.Front;
        var back = analysis.Centering.Back;
        if (centeringDefects.Length > 0)
        {
            if (centeringDefects.Any(d => d.Side == CardSide.Front && !DefectNarrator.IsActive(d)))
            {
                front = Perfect(front);
            }

            if (centeringDefects.Any(d => d.Side == CardSide.Back && !DefectNarrator.IsActive(d)))
            {
                back = Perfect(back);
            }
        }

        var cap = centeringDefects.Length > 0
            ? CenteringGradeCapCalculator.CalculateCap(front, back, profile)
            : analysis.Centering.EstimatedGradeCap;
        var score = front is null ? 0 : CenteringMath.ConditionScore(front.WorstRatio);
        return (score, cap);
    }

    private static CenteringMeasurement? Perfect(CenteringMeasurement? original) =>
        original is null ? null : CenteringMath.FromMargins(50, 50, 50, 50, original.Confidence, original.Guide);

    private static (double Score, int? Cap) ResolveCategory(
        AnalysisStatus status,
        double storedScore,
        int? storedCap,
        IEnumerable<DetectedDefect> defects)
    {
        if (status != AnalysisStatus.Completed)
        {
            return (storedScore, null);
        }

        var list = defects.ToArray();
        var ignoredAny = list.Any(d => !DefectNarrator.IsActive(d));
        var cap = ignoredAny ? DefectScoreMath.GradeCap(list) : storedCap ?? DefectScoreMath.GradeCap(list);
        var score = ignoredAny
            ? (list.Any(DefectNarrator.IsActive) ? DefectScoreMath.ConditionScore(list) : 100)
            : storedScore;
        return (score, cap);
    }

    private static string[] ActiveNotes(IEnumerable<DetectedDefect> defects) =>
        defects.Where(DefectNarrator.IsActive).Select(FormatDefect).ToArray();

    private static string FormatDefect(DetectedDefect defect)
    {
        if (!string.IsNullOrWhiteSpace(defect.Title) && !string.IsNullOrWhiteSpace(defect.Impact))
        {
            return $"{defect.Title}. {defect.Impact}";
        }

        return defect.Description;
    }

    private static CategoryGrade ToCategory(
        string name,
        AnalysisStatus status,
        double score,
        int? cap,
        double confidence,
        string? unavailable,
        IReadOnlyList<string> notes) =>
        new()
        {
            Name = name,
            Status = status,
            ConditionScore = score,
            GradeCap = status == AnalysisStatus.Completed ? cap : null,
            Confidence = confidence,
            UnavailableReason = unavailable,
            Notes = notes
        };

    private static int ScoreToGrade(double score, GradingProfile profile)
    {
        foreach (var grade in Enumerable.Range(1, 10).Reverse())
        {
            if (profile.GradeThresholds.TryGetValue(grade.ToString(), out var threshold) && score >= threshold)
            {
                return grade;
            }
        }

        return 1;
    }

    private static double ThresholdFor(int grade, GradingProfile profile) =>
        profile.GradeThresholds.TryGetValue(grade.ToString(), out var value) ? value : 0;

    private static bool IsBorderline(CenteringMeasurement? front, CenteringMeasurement? back, GradingProfile profile)
    {
        return IsNearThreshold(front?.WorstRatio, isFront: true, profile)
               || IsNearThreshold(back?.WorstRatio, isFront: false, profile);
    }

    private static bool IsNearThreshold(double? worst, bool isFront, GradingProfile profile)
    {
        if (worst is null)
        {
            return false;
        }

        foreach (var threshold in profile.Centering.Values)
        {
            var max = isFront ? threshold.FrontMax : threshold.BackMax;
            if (Math.Abs(worst.Value - max) <= profile.BorderlineDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static double ComputeConfidence(CardAnalysisResult analysis)
    {
        var quality = analysis.Front?.Quality.OverallQualityScore / 100.0 ?? 0.4;
        var detection = analysis.Front?.Detection.Confidence ?? 0.4;
        var coverage = Math.Clamp(analysis.Coverage.Overall / 100.0, 0.2, 1.0);
        var backBonus = analysis.Back is null ? 0.85 : 1.0;
        var centeringConfidence = analysis.Centering.Front?.Confidence ?? 0.4;
        var cornerConfidence = analysis.Corners.Status == AnalysisStatus.Completed
            ? 0.5 + (analysis.Corners.Corners.Count(c => c.UsedMacroImage) * 0.08)
            : 0.4;
        var surfaceConfidence = analysis.Surface.Status == AnalysisStatus.Completed
            ? analysis.Surface.Confidence
            : 0.4;
        var value = quality * detection * Math.Max(coverage, 0.25) * backBonus
                    * (0.4 + (centeringConfidence * 0.25) + (cornerConfidence * 0.2) + (surfaceConfidence * 0.15));
        return Math.Clamp(value * 100.0, 5, 95);
    }
}
