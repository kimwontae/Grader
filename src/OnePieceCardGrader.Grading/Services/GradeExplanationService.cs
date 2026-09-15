using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Explanations;
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
                positives.Add($"전면 센터링은 PSA 10 기준 안입니다. 좌/우 {front.LeftPercent:F1}/{front.RightPercent:F1}, 상/하 {front.TopPercent:F1}/{front.BottomPercent:F1}.");
            }
        }

        if (analysis.Centering.Back is { } back)
        {
            positives.Add($"후면 센터링 좌/우 {back.LeftPercent:F1}/{back.RightPercent:F1}, 상/하 {back.TopPercent:F1}/{back.BottomPercent:F1}.");
        }

        foreach (var corner in analysis.Corners.Corners)
        {
            if (corner.UsedManualRegion)
            {
                positives.Add($"{DefectNarrator.LocationKo(corner.Position.ToString())}은 직접 지정한 카드 영역으로 분석했습니다.");
            }
            else if (corner.UsedMacroImage)
            {
                positives.Add($"{DefectNarrator.LocationKo(corner.Position.ToString())} 확대 사진을 사용했습니다.");
            }
        }

        foreach (var defect in result.Defects.Where(DefectNarrator.IsActive).OrderByDescending(d => d.Severity).ThenByDescending(d => d.Confidence).Take(6))
        {
            limits.Add(string.IsNullOrWhiteSpace(defect.Impact)
                ? defect.Description
                : $"{defect.Title}. {defect.Description} {defect.Impact}");
        }

        if (analysis.Surface.UsedMacroFallback)
        {
            limits.Add("표면 클로즈업/사광 사진이 없어 전체 사진으로 표면을 분석했습니다. 사광 사진을 추가하면 스크래치 검출 정확도가 올라갑니다.");
        }

        if (analysis.Front?.Quality.GlareCoverageRatio > 0.08)
        {
            limits.Add($"표면 반사광이 {analysis.Front.Quality.GlareCoverageRatio:P0}를 가려 미세 스크래치 검출 정확도가 낮습니다.");
        }

        var combined = limits.Distinct().Take(6).ToArray();
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
