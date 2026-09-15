using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging;
using OnePieceCardGrader.Imaging.Quality;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Tests;

public sealed class ImageQualityAnalyzerTests
{
    [Fact]
    public void SharpImage_ShouldScoreHigherFocusThanBlurredImage()
    {
        using var sharp = new Mat(1600, 1200, MatType.CV_8UC3, new Scalar(30, 30, 30));
        Cv2.Rectangle(sharp, new Rect(200, 150, 800, 1100), new Scalar(200, 180, 40), 6);
        using var blur = sharp.GaussianBlur(new Size(31, 31), 12);

        var analyzer = new ImageQualityAnalyzer(new AnalysisOptions());
        var sharpResult = analyzer.Analyze(MatAdapter.Wrap(sharp));
        var blurResult = analyzer.Analyze(MatAdapter.Wrap(blur));

        Assert.True(sharpResult.FocusScore > blurResult.FocusScore);
        Assert.InRange(sharpResult.ResolutionScore, 70, 100);
    }
}
