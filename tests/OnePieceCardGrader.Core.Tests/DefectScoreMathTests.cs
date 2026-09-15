using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Core.Tests;

public sealed class DefectScoreMathTests
{
    [Fact]
    public void MinorWhitening_ShouldCapAtNine()
    {
        var cap = DefectScoreMath.GradeCap(
        [
            new DetectedDefect
            {
                Type = DefectType.CornerWhitening,
                Severity = DefectSeverity.Minor,
                Confidence = 0.7,
                GradeCap = 9
            }
        ]);
        Assert.Equal(9, cap);
    }

    [Fact]
    public void LowConfidenceDefect_ShouldNotDropCapAsHard()
    {
        var cap = DefectScoreMath.GradeCap(
        [
            new DetectedDefect
            {
                Type = DefectType.Scratch,
                Severity = DefectSeverity.Moderate,
                Confidence = 0.3,
                GradeCap = 8
            }
        ]);
        Assert.Equal(9, cap);
    }
}
