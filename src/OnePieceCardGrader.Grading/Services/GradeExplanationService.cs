using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Grading.Services;

public sealed class GradeExplanationService : IGradeExplanationService
{
    public GradeExplanation Generate(CardAnalysisResult analysis, GradingResult result)
    {
        var limits = new List<string>();
        var positives = new List<string>();

        if (analysis.Centering.Front is { } front)
        {
            if (result.Centering.GradeCap >= 10)
            {
                positives.Add($"Centering은 PSA 10 공개 기준 범위 안으로 측정되었습니다. Front L/R {front.LeftPercent:F1}/{front.RightPercent:F1}, T/B {front.TopPercent:F1}/{front.BottomPercent:F1}.");
            }
            else
            {
                limits.Add($"Front centering worst {front.WorstRatio:F1}% (L/R {front.LeftPercent:F1}/{front.RightPercent:F1}, T/B {front.TopPercent:F1}/{front.BottomPercent:F1})가 PSA 10 기준을 벗어났습니다.");
            }
        }

        if (analysis.Centering.Back is { } back)
        {
            positives.Add($"Back centering L/R {back.LeftPercent:F1}/{back.RightPercent:F1}, T/B {back.TopPercent:F1}/{back.BottomPercent:F1}.");
        }

        foreach (var note in analysis.Centering.Notes)
        {
            limits.Add(note);
        }

        limits.Add("Corner / Edge / Surface 자동 분석은 아직 구현되지 않았습니다. 현재 예상 등급은 센터링과 이미지 품질만 반영합니다.");

        if (analysis.Front?.Quality.GlareCoverageRatio > 0.08)
        {
            limits.Add($"표면 반사광이 {analysis.Front.Quality.GlareCoverageRatio:P0}를 가려 미세 스크래치 검출 정확도가 낮습니다.");
        }

        var ordered = result.Defects
            .OrderByDescending(d => d.Severity)
            .ThenByDescending(d => d.Confidence)
            .Take(5)
            .Select(d => d.Description)
            .ToList();

        limits.InsertRange(0, ordered);

        var summary =
            $"PSA 예상 등급 {result.PredictedGrade} (범위 {result.GradeRangeMin}~{result.GradeRangeMax}). " +
            AppConstants.DisclaimerKo;

        return new GradeExplanation
        {
            Summary = summary,
            LimitReasons = limits.Distinct().Take(5).ToArray(),
            PrimaryTenLimiter = result.PrimaryTenLimiter,
            PositiveNotes = positives
        };
    }
}
