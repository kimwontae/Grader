using Microsoft.Extensions.Logging.Abstractions;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging;
using OnePieceCardGrader.Imaging.Corners;
using OnePieceCardGrader.Imaging.Whitening;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Tests;

public sealed class CornerAnalyzerTests
{
    [Fact]
    public void PatchyBrightEdge_ShouldIncreaseWhiteningSeverity()
    {
        using var clean = new Mat(1760, 1260, MatType.CV_8UC3, new Scalar(40, 50, 60));
        using var damaged = clean.Clone();
        Cv2.Rectangle(damaged, new Rect(0, 0, 1260, 16), new Scalar(250, 250, 250), -1);
        Cv2.Rectangle(damaged, new Rect(0, 0, 16, 1760), new Scalar(250, 250, 250), -1);

        var options = new ImageAnalysisOptions();
        var analysis = new AnalysisOptions();
        var whitening = new WhiteningAnalyzer(options, NullLogger<WhiteningAnalyzer>.Instance);
        var geometry = new CornerGeometryAnalyzer(options, NullLogger<CornerGeometryAnalyzer>.Instance);
        var analyzer = new CornerAnalyzer(options, analysis, whitening, geometry, NullLogger<CornerAnalyzer>.Instance);

        var cleanResult = analyzer.Analyze(MatAdapter.Wrap(clean), CornerPosition.TopLeft, CardSide.Front, false);
        var damagedResult = analyzer.Analyze(MatAdapter.Wrap(damaged), CornerPosition.TopLeft, CardSide.Front, false);

        Assert.Equal(AnalysisStatus.Completed, damagedResult.Status);
        Assert.True(damagedResult.WhiteningSeverity > cleanResult.WhiteningSeverity);
        Assert.True(damagedResult.CombinedScore <= cleanResult.CombinedScore);
    }
}
