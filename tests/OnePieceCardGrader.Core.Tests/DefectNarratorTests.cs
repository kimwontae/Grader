using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Explanations;

namespace OnePieceCardGrader.Core.Tests;

public sealed class DefectNarratorTests
{
    [Fact]
    public void CenteringNarration_ShouldExplainMmErrorAndPsa10Target()
    {
        var measurement = CenteringMath.FromMargins(70, 30, 50, 50, 0.9);
        var narration = DefectNarrator.ForCentering(
            CardSide.Front,
            measurement,
            gradeCap: 9,
            cardWidthMm: 63,
            cardHeightMm: 88,
            imageWidthPx: 1260,
            imageHeightPx: 1760,
            psa10MaxPercent: 55);

        Assert.Contains("mm", narration.Description);
        Assert.Contains("왼쪽", narration.Description);
        Assert.Contains("오른쪽", narration.Description);
        Assert.Contains("PSA 10", narration.Impact);
        Assert.Contains("55", narration.Impact);
        Assert.Contains("9", narration.Impact);
    }
}
