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

        foreach (var corner in analysis.Corners.Corners)
        {
            if (corner.UsedMacroImage)
            {
                positives.Add($"{corner.Position} 확대 사진을 사용했습니다.");
            }

            foreach (var defect in corner.Defects)
            {
                limits.Add(defect.Description);
            }
        }

        foreach (var defect in analysis.Surface.Defects.OrderByDescending(d => d.Severity).Take(3))
        {
            limits.Add(defect.Description);
        }

        if (analysis.Surface.UsedMacroFallback)
        {
            limits.Add("표면 클로즈업/사광 사진이 없어 전체 사진으로 surface를 분석했습니다. 사광 사진을 추가하면 스크래치 검출 정확도가 올라갑니다.");
        }

        if (analysis.Front?.Quality.GlareCoverageRatio > 0.08)
        {
            limits.Add($"표면 반사광이 {analysis.Front.Quality.GlareCoverageRatio:P0}를 가려 미세 스크래치 검출 정확도가 낮습니다.");
        }

        var ordered = result.Defects
            .OrderByDescending(d => d.Severity)
            .ThenByDescending(d => d.Confidence)
            .Take(5)
            .Select(d => d.Description);

        var combined = ordered.Concat(limits).Distinct().Take(5).ToArray();
        var summary =
            $"PSA 예상 등급 {result.PredictedGrade} (범위 {result.GradeRangeMin}~{result.GradeRangeMax}). " +
            AppConstants.DisclaimerKo;

        return new GradeExplanation
        {
            Summary = summary,
            LimitReasons = combined,
            PrimaryTenLimiter = result.PrimaryTenLimiter,
            PositiveNotes = positives.Distinct().ToArray()
        };
    }
}
