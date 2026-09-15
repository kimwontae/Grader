using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Core.Tests;

public sealed class QuadCornersTests
{
    [Fact]
    public void FromUnordered_ShouldOrderClockwiseFromTopLeft()
    {
        var unordered = new[]
        {
            new ImagePoint(80, 90),
            new ImagePoint(10, 12),
            new ImagePoint(12, 88),
            new ImagePoint(85, 15)
        };

        var quad = QuadCorners.FromUnordered(unordered);
        Assert.Equal(10, quad.TopLeft.X, 3);
        Assert.Equal(12, quad.TopLeft.Y, 3);
        Assert.Equal(85, quad.TopRight.X, 3);
        Assert.Equal(15, quad.TopRight.Y, 3);
        Assert.Equal(80, quad.BottomRight.X, 3);
        Assert.Equal(90, quad.BottomRight.Y, 3);
        Assert.Equal(12, quad.BottomLeft.X, 3);
        Assert.Equal(88, quad.BottomLeft.Y, 3);
    }
}
