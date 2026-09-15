using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Core.Tests;

public sealed class CenteringMathTests
{
    [Fact]
    public void Centering_50_50_ShouldReturnPerfectScore()
    {
        var measurement = CenteringMath.FromMargins(10, 10, 10, 10, 1);
        Assert.Equal(50, measurement.LeftPercent, 3);
        Assert.Equal(50, measurement.RightPercent, 3);
        Assert.Equal(50, measurement.TopPercent, 3);
        Assert.Equal(50, measurement.BottomPercent, 3);
        Assert.Equal(50, measurement.WorstRatio, 3);
        Assert.Equal(100, CenteringMath.ConditionScore(measurement.WorstRatio), 3);
    }

    [Theory]
    [InlineData(55, 45)]
    [InlineData(52.3, 47.7)]
    public void LeftRight_Split_ShouldMatchExpectedPercents(double left, double right)
    {
        var measurement = CenteringMath.FromMargins(left, right, 10, 10, 1);
        Assert.Equal(left / (left + right) * 100, measurement.LeftPercent, 3);
        Assert.Equal(right / (left + right) * 100, measurement.RightPercent, 3);
    }

    [Fact]
    public void Guide_ShouldProduceMatchingMargins()
    {
        var guide = new CenteringGuide
        {
            PrintLeft = 0.10,
            PrintRight = 0.90,
            PrintTop = 0.20,
            PrintBottom = 0.80
        };

        var measurement = CenteringMath.FromGuide(guide, 1000, 1000, 0.8);
        Assert.Equal(50, measurement.LeftPercent, 3);
        Assert.Equal(50, measurement.RightPercent, 3);
        Assert.Equal(50, measurement.TopPercent, 3);
        Assert.Equal(50, measurement.BottomPercent, 3);
    }
}
