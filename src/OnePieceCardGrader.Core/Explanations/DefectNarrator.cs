using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Core.Explanations;

public sealed record DefectNarration(string Title, string Description, string Impact);

public static class DefectNarrator
{
    public static DefectNarration ForCentering(
        CardSide side,
        CenteringMeasurement measurement,
        int gradeCap,
        double cardWidthMm,
        double cardHeightMm,
        double imageWidthPx,
        double imageHeightPx,
        double psa10MaxPercent)
    {
        var sideKo = SideKo(side);
        var leftMm = ToMm(measurement.LeftMarginPx, imageWidthPx, cardWidthMm);
        var rightMm = ToMm(measurement.RightMarginPx, imageWidthPx, cardWidthMm);
        var topMm = ToMm(measurement.TopMarginPx, imageHeightPx, cardHeightMm);
        var bottomMm = ToMm(measurement.BottomMarginPx, imageHeightPx, cardHeightMm);

        var horizontalDiff = Math.Abs(leftMm - rightMm);
        var verticalDiff = Math.Abs(topMm - bottomMm);
        var horizontalTotal = leftMm + rightMm;
        var verticalTotal = topMm + bottomMm;
        var allowedRatio = (psa10MaxPercent - (100.0 - psa10MaxPercent)) / 100.0;
        var allowedHorizontal = allowedRatio * horizontalTotal;
        var allowedVertical = allowedRatio * verticalTotal;
        var minPercent = 100.0 - psa10MaxPercent;
        var worstIsHorizontal = Math.Max(measurement.LeftPercent, measurement.RightPercent)
                                >= Math.Max(measurement.TopPercent, measurement.BottomPercent);
        var heavySide = measurement.LeftPercent >= measurement.RightPercent ? "왼쪽" : "오른쪽";
        if (!worstIsHorizontal)
        {
            heavySide = measurement.TopPercent >= measurement.BottomPercent ? "위쪽" : "아래쪽";
        }

        var title = $"{sideKo} 센터링이 {heavySide}으로 치우쳐 있습니다";
        var description =
            $"{sideKo} 테두리 두께는 왼쪽 {leftMm:F2}mm, 오른쪽 {rightMm:F2}mm이고, " +
            $"위 {topMm:F2}mm, 아래 {bottomMm:F2}mm입니다. " +
            $"좌우 차이는 {horizontalDiff:F2}mm(비율 {measurement.LeftPercent:F1}/{measurement.RightPercent:F1}), " +
            $"상하 차이는 {verticalDiff:F2}mm(비율 {measurement.TopPercent:F1}/{measurement.BottomPercent:F1})입니다. " +
            $"가장 기울어진 쪽은 {measurement.WorstRatio:F1}%입니다.";

        var needed = worstIsHorizontal
            ? $"좌우 테두리 차이를 {horizontalDiff:F2}mm에서 {allowedHorizontal:F2}mm 이하로 줄여야 합니다."
            : $"상하 테두리 차이를 {verticalDiff:F2}mm에서 {allowedVertical:F2}mm 이하로 줄여야 합니다.";

        var impact = gradeCap >= 10
            ? $"이 센터링은 PSA 10 기준({psa10MaxPercent:F0}/{minPercent:F0}) 안입니다."
            : $"이 오차 때문에 센터링만 보면 예상 등급은 PSA {gradeCap}까지입니다. " +
              $"PSA 10을 받으려면 {sideKo} 비율이 {psa10MaxPercent:F0}/{minPercent:F0} 이내여야 합니다. {needed}";

        return new DefectNarration(title, description, impact);
    }

    public static DefectNarration ForWhitening(
        CardSide side,
        string location,
        DefectType type,
        DefectSeverity severity,
        WhiteningAnalysisResult result,
        int? gradeCap)
    {
        var place = $"{SideKo(side)} {LocationKo(location)}";
        var kind = type == DefectType.EdgeWhitening ? "엣지" : "모서리";
        var title = $"{place}에 {SeverityKo(severity)} 화이트닝이 있습니다";
        var lengthPct = result.WhiteningLengthRatio * 100.0;
        var areaPct = result.WhiteningAreaRatio * 100.0;
        var description =
            $"{place}에서 카드 원래 색보다 하얗게 마모된 구간이 감지되었습니다. " +
            $"해당 {kind} 길이의 약 {lengthPct:F1}%가 색이 바뀌었고, " +
            $"마모 면적은 약 {areaPct:F2}%입니다. " +
            $"주변 색과 비교한 색 차이(ΔE)는 평균 {result.MeanDeltaE:F1}, 최대 {result.MaxDeltaE:F1}입니다. " +
            $"문제 구간은 {result.DefectCount}곳입니다.";
        var impact = GradeImpact(gradeCap, "해당 부위에 눈에 띄는 흰 마모가 없어야 합니다.");
        return new DefectNarration(title, description, impact);
    }

    public static DefectNarration ForGeometry(CardSide side, CornerGeometryResult result, DefectSeverity severity, int? gradeCap)
    {
        var place = $"{SideKo(side)} {LocationKo(result.Position.ToString())}";
        var shape = GeometryKo(result.Type);
        var title = $"{place}이(가) {shape}";
        var missingPct = result.MissingAreaRatio * 100.0;
        var description =
            $"{place} 형태를 이상적인 직각 모서리와 비교했습니다. " +
            $"{shape} 상태로 보이며, 부족한 면적은 약 {missingPct:F2}%입니다. " +
            $"윤곽이 기준선에서 평균 {result.MeanContourDeviation:F3}만큼 벗어나 있습니다.";
        var impact = GradeImpact(gradeCap, "모서리 형태가 깨지거나 둥글게 무너지지 않아야 합니다.");
        return new DefectNarration(title, description, impact);
    }

    public static DefectNarration ForScratch(CardSide side, ScratchCandidate candidate, DefectSeverity severity, int? gradeCap)
    {
        var place = SideKo(side);
        var kind = candidate.Type == ScratchCandidateType.PrintLineCandidate ? "인쇄 라인처럼 보이는 선" : "스크래치";
        var title = $"{place} 표면에 {SeverityKo(severity)} {kind}가 있습니다";
        var lengthPct = candidate.LengthRatio * 100.0;
        var widthPct = candidate.WidthRatio * 100.0;
        var description =
            $"{place} 표면에서 {kind} 후보가 감지되었습니다. " +
            $"길이는 카드 기준으로 약 {lengthPct:F2}%, 두께는 약 {widthPct:F3}%이며, " +
            $"주변과 대비는 {candidate.LocalContrast:F1}입니다. " +
            "사진 위 확대 이미지에서 표시된 사각형이 해당 위치입니다.";
        var impact = GradeImpact(gradeCap, "해당 위치의 선 흠집이나 스크래치가 보이지 않아야 합니다.");
        return new DefectNarration(title, description, impact);
    }

    public static string SideKo(CardSide side) => side == CardSide.Back ? "후면" : "전면";

    public static string LocationKo(string? label) => label switch
    {
        "TopLeft" => "좌상단 모서리",
        "TopRight" => "우상단 모서리",
        "BottomRight" => "우하단 모서리",
        "BottomLeft" => "좌하단 모서리",
        "Top" => "상단 변",
        "Right" => "우측 변",
        "Bottom" => "하단 변",
        "Left" => "좌측 변",
        _ => string.IsNullOrWhiteSpace(label) ? "해당 위치" : label
    };

    public static string SeverityKo(DefectSeverity severity) => severity switch
    {
        DefectSeverity.Severe => "매우 심한",
        DefectSeverity.Major => "심한",
        DefectSeverity.Moderate => "뚜렷한",
        DefectSeverity.Minor => "약한",
        DefectSeverity.Trace => "아주 약한",
        _ => ""
    };

    public static bool IsActive(DetectedDefect defect) =>
        defect.ReviewStatus is not DefectReviewStatus.FalsePositive and not DefectReviewStatus.Ignored;

    private static string GeometryKo(CornerGeometryType type) => type switch
    {
        CornerGeometryType.Chipping => "깨져 있습니다",
        CornerGeometryType.MissingMaterial => "일부가 없습니다",
        CornerGeometryType.Rounding => "둥글게 마모되어 있습니다",
        CornerGeometryType.Blunting => "무뎌져 있습니다",
        CornerGeometryType.RoughCut => "재단이 거칠습니다",
        CornerGeometryType.FrayingCandidate => "섬유가 일어난 흔적이 있습니다",
        _ => "형태가 변형되어 있습니다"
    };

    private static string GradeImpact(int? gradeCap, string psa10Need)
    {
        if (gradeCap is null or >= 10)
        {
            return $"이 정도면 PSA 10을 막는 치명적인 결함은 아닙니다. 다만 다른 항목과 겹치면 등급이 내려갈 수 있습니다.";
        }

        return $"이 결함만 봐도 예상 등급은 PSA {gradeCap}을 넘기기 어렵습니다. PSA 10을 받으려면 {psa10Need}";
    }

    private static double ToMm(double pixels, double imagePixels, double cardMm)
    {
        if (imagePixels <= 0 || cardMm <= 0)
        {
            return 0;
        }

        return pixels / imagePixels * cardMm;
    }
}
