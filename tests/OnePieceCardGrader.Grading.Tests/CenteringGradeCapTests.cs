using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Grading.Profiles;

namespace OnePieceCardGrader.Grading.Tests;

public sealed class CenteringGradeCapTests
{
    private readonly GradingProfile _profile = JsonGradingProfileProvider.CreateDefaultPsa();

    [Fact]
    public void Psa10_Front_55_45_ShouldPassRuleEngineThreshold()
    {
        var front = CenteringMath.FromMargins(55, 45, 50, 50, 1);
        var cap = CenteringGradeCapCalculator.CalculateCap(front, null, _profile);
        Assert.Equal(10, cap);
    }

    [Fact]
    public void Psa10_Front_56_44_ShouldFailStrictThreshold()
    {
        var front = CenteringMath.FromMargins(56, 44, 50, 50, 1);
        var cap = CenteringGradeCapCalculator.CalculateCap(front, null, _profile);
        Assert.Equal(9, cap);
    }

    [Fact]
    public void Back_75_25_ShouldStillAllowPsa10_WhenFrontIsPerfect()
    {
        var front = CenteringMath.FromMargins(50, 50, 50, 50, 1);
        var back = CenteringMath.FromMargins(75, 25, 50, 50, 1);
        var cap = CenteringGradeCapCalculator.CalculateCap(front, back, _profile);
        Assert.Equal(10, cap);
    }

    [Fact]
    public void Back_76_24_ShouldCapBelowTen()
    {
        var front = CenteringMath.FromMargins(50, 50, 50, 50, 1);
        var back = CenteringMath.FromMargins(76, 24, 50, 50, 1);
        var cap = CenteringGradeCapCalculator.CalculateCap(front, back, _profile);
        Assert.True(cap <= 9);
    }
}
