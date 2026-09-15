using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Enums;
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

        var centeringScore = analysis.Centering.Front is null
            ? 0
            : CenteringMath.ConditionScore(analysis.Centering.Front.WorstRatio);
        var centeringCap = analysis.Centering.EstimatedGradeCap;
        var centeringGrade = new CategoryGrade
        {
            Name = "Centering",
            Status = analysis.Centering.Status,
            ConditionScore = centeringScore,
            GradeCap = centeringCap,
            Confidence = analysis.Centering.Front?.Confidence ?? 0,
            Notes = analysis.Centering.Notes
        };

        var notImplemented = new CategoryGrade
        {
            Status = AnalysisStatus.NotImplemented,
            UnavailableReason = "이 항목의 분석은 아직 구현되지 않았습니다.",
            ConditionScore = 0,
            Confidence = 0
        };

        var compositeFromCentering = ScoreToGrade(centeringScore, profile);
        var predicted = Math.Min(compositeFromCentering, centeringCap);

        var unimplementedPenalty = 2;
        var rangeMin = Math.Max(AppConstants.MinGrade, predicted - unimplementedPenalty);
        var rangeMax = predicted;
        var borderline = IsBorderline(analysis.Centering.Front, analysis.Centering.Back, profile);
        if (borderline)
        {
            rangeMax = Math.Min(AppConstants.MaxGrade, predicted + 1);
            rangeMin = Math.Max(AppConstants.MinGrade, predicted - 1);
        }

        var confidence = ComputeConfidence(analysis);
        var reasons = new List<string>();
        if (centeringCap < 10)
        {
            reasons.Add($"센터링 worst ratio가 PSA 10 기준을 초과하여 예상 상한이 {centeringCap}입니다.");
        }

        reasons.Add("Corner / Edge / Surface 분석이 아직 구현되지 않아 최종 예상은 센터링 중심으로 보수적으로 산출되었습니다.");

        _logger.LogInformation(
            "Grading engine predicted {Grade} (range {Min}-{Max}, confidence {Confidence:F0})",
            predicted,
            rangeMin,
            rangeMax,
            confidence);

        return new GradingResult
        {
            PredictedGrade = predicted,
            GradeRangeMin = rangeMin,
            GradeRangeMax = Math.Max(rangeMax, predicted),
            AnalysisConfidence = confidence,
            Centering = centeringGrade,
            Corners = notImplemented with { Name = "Corners" },
            Edges = notImplemented with { Name = "Edges" },
            Surface = notImplemented with { Name = "Surface" },
            Defects = analysis.Defects,
            GradeLimitReasons = reasons,
            Summary = $"센터링 기준 예상 PSA {predicted} (범위 {rangeMin}~{Math.Max(rangeMax, predicted)})",
            IsBorderline = borderline,
            PrimaryTenLimiter = centeringCap < 10
                ? "센터링이 PSA 10 공개 기준 범위를 벗어났습니다."
                : "Corner/Edge/Surface가 미구현이므로 PSA 10을 확정할 수 없습니다."
        };
    }

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
        var value = quality * detection * Math.Max(coverage, 0.25) * backBonus * (0.5 + (centeringConfidence * 0.5));
        return Math.Clamp(value * 100.0, 5, 95);
    }
}
