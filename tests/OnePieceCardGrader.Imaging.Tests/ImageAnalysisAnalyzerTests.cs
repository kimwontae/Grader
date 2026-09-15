using Microsoft.Extensions.Logging.Abstractions;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging;
using OnePieceCardGrader.Imaging.Corners;
using OnePieceCardGrader.Imaging.Surface;
using OnePieceCardGrader.Imaging.Whitening;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Tests;

public sealed class WhiteningAnalyzerTests
{
    [Fact]
    public void SyntheticWhitening_ShouldIncreaseWithLongerDefect()
    {
        var analyzer = CreateAnalyzer();
        using var clean = CreateCard();
        using var small = CreateCard();
        using var longEdge = CreateCard();
        Cv2.Rectangle(small, new Rect(20, 0, 28, 8), new Scalar(250, 250, 250), -1);
        Cv2.Rectangle(longEdge, new Rect(10, 0, 360, 10), new Scalar(250, 250, 250), -1);

        var none = analyzer.Analyze(clean, WhiteningRegion.TopEdge);
        var shortDefect = analyzer.Analyze(small, WhiteningRegion.TopEdge);
        var longDefect = analyzer.Analyze(longEdge, WhiteningRegion.TopEdge);

        Assert.True(none.Severity < 0.08, $"clean severity {none.Severity}");
        Assert.True(shortDefect.Severity > none.Severity);
        Assert.True(longDefect.Severity > shortDefect.Severity);
        Assert.True(longDefect.WhiteningLengthRatio > shortDefect.WhiteningLengthRatio);
        Assert.True(longDefect.Components.Count >= 1);
    }

    [Fact]
    public void InteriorPrintedWhite_ShouldNotCountAsEdgeWhitening()
    {
        var analyzer = CreateAnalyzer();
        using var card = CreateCard();
        Cv2.Rectangle(card, new Rect(80, 180, 220, 160), new Scalar(250, 250, 250), -1);
        var result = analyzer.Analyze(card, WhiteningRegion.TopEdge);
        Assert.True(result.Severity < 0.12, $"interior print severity {result.Severity} count={result.DefectCount}");
    }

    private static WhiteningAnalyzer CreateAnalyzer() =>
        new(new ImageAnalysisOptions(), NullLogger<WhiteningAnalyzer>.Instance);

    private static Mat CreateCard()
    {
        var card = new Mat(560, 400, MatType.CV_8UC3, new Scalar(42, 58, 96));
        Cv2.Rectangle(card, new Rect(18, 18, 364, 524), new Scalar(36, 48, 78), -1);
        return card;
    }
}

public sealed class CornerGeometryAnalyzerTests
{
    [Fact]
    public void ChipSize_ShouldIncreaseGeometrySeverity()
    {
        var analyzer = new CornerGeometryAnalyzer(new ImageAnalysisOptions(), NullLogger<CornerGeometryAnalyzer>.Instance);
        using var normal = CreateRoundedCard(radius: 7);
        using var smallChip = CreateRoundedCard(radius: 7);
        using var largeChip = CreateRoundedCard(radius: 7);
        Cv2.FillConvexPoly(smallChip, new[] { new Point(0, 0), new Point(16, 0), new Point(0, 16) }, new Scalar(0, 0, 0));
        Cv2.FillConvexPoly(largeChip, new[] { new Point(0, 0), new Point(42, 0), new Point(0, 42) }, new Scalar(0, 0, 0));

        var normalResult = analyzer.Analyze(normal, CornerPosition.TopLeft);
        var smallResult = analyzer.Analyze(smallChip, CornerPosition.TopLeft);
        var largeResult = analyzer.Analyze(largeChip, CornerPosition.TopLeft);

        Assert.True(normalResult.Severity < smallResult.Severity, $"normal {normalResult.Severity} small {smallResult.Severity}");
        Assert.True(smallResult.Severity < largeResult.Severity, $"small {smallResult.Severity} large {largeResult.Severity}");
        Assert.True(largeResult.MissingAreaRatio > smallResult.MissingAreaRatio);
    }

    private static Mat CreateRoundedCard(int radius)
    {
        var card = new Mat(560, 400, MatType.CV_8UC3, new Scalar(0, 0, 0));
        FillRoundedRect(card, new Rect(0, 0, 400, 560), radius, new Scalar(40, 70, 110));
        return card;
    }

    private static void FillRoundedRect(Mat image, Rect rect, int radius, Scalar color)
    {
        var r = Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2);
        Cv2.Rectangle(image, new Rect(rect.X + r, rect.Y, rect.Width - (2 * r), rect.Height), color, -1);
        Cv2.Rectangle(image, new Rect(rect.X, rect.Y + r, rect.Width, rect.Height - (2 * r)), color, -1);
        Cv2.Circle(image, new Point(rect.X + r, rect.Y + r), r, color, -1);
        Cv2.Circle(image, new Point(rect.X + rect.Width - r, rect.Y + r), r, color, -1);
        Cv2.Circle(image, new Point(rect.X + r, rect.Y + rect.Height - r), r, color, -1);
        Cv2.Circle(image, new Point(rect.X + rect.Width - r, rect.Y + rect.Height - r), r, color, -1);
    }
}

public sealed class ScratchAnalyzerTests
{
    [Fact]
    public void CandidateMask_ShouldContainHorizontalDarkLine()
    {
        using var gray = new Mat(120, 220, MatType.CV_8UC1, new Scalar(96));
        Cv2.Line(gray, new Point(12, 60), new Point(208, 62), Scalar.All(12), 2);
        using var glare = new Mat(gray.Size(), MatType.CV_8UC1, Scalar.All(0));
        var detector = new ScratchCandidateDetector(new ScratchAnalysisOptions());
        using var masks = detector.Detect(gray, glare);
        Assert.True(Cv2.CountNonZero(masks.CandidateMask) > 30, $"mask pixels={Cv2.CountNonZero(masks.CandidateMask)}");
    }

    [Fact]
    public void ThinLine_ShouldScoreHigherThanGlareBlob()
    {
        var analyzer = new ScratchAnalyzer(new ImageAnalysisOptions(), new AnalysisOptions(), NullLogger<ScratchAnalyzer>.Instance);
        var nonePath = WriteCard("none", DrawNone);
        var linePath = WriteCard("line", DrawThinLine);
        var glarePath = WriteCard("glare", DrawGlare);

        try
        {
            var none = analyzer.Analyze(new Core.Models.SurfaceImageSet { NormalFrontPath = nonePath }, CardSide.Front);
            var line = analyzer.Analyze(new Core.Models.SurfaceImageSet { NormalFrontPath = linePath }, CardSide.Front);
            var glare = analyzer.Analyze(new Core.Models.SurfaceImageSet { NormalFrontPath = glarePath }, CardSide.Front);

            using var lineBgr = Cv2.ImRead(linePath);
            using var lineGray = lineBgr.CvtColor(ColorConversionCodes.BGR2GRAY);
            using var emptyGlare = new Mat(lineGray.Size(), MatType.CV_8UC1, Scalar.All(0));
            var detector = new ScratchCandidateDetector(new ScratchAnalysisOptions());
            using var masks = detector.Detect(lineGray, emptyGlare);
            var maskPixels = Cv2.CountNonZero(masks.CandidateMask);

            Assert.True(line.Severity > none.Severity, $"line {line.Severity} none {none.Severity} candidates={line.CandidateCount} mask={maskPixels}");
            Assert.True(line.CandidateCount >= 1);
            Assert.True(glare.WorstScratchSeverity <= line.WorstScratchSeverity + 0.05);
            Assert.All(glare.Candidates, c => Assert.True(c.GlareOverlap < 0.9 || c.DetectionConfidence < 0.5));
        }
        finally
        {
            File.Delete(nonePath);
            File.Delete(linePath);
            File.Delete(glarePath);
        }
    }

    private static string WriteCard(string name, Action<Mat> draw)
    {
        using var image = new Mat(640, 480, MatType.CV_8UC3);
        var rng = new Random(7);
        var indexer = image.GetGenericIndexer<Vec3b>();
        for (var y = 0; y < image.Rows; y++)
        {
            for (var x = 0; x < image.Cols; x++)
            {
                var n = (byte)(88 + rng.Next(-3, 4));
                indexer[y, x] = new Vec3b(n, (byte)(n + 6), (byte)(n + 12));
            }
        }

        draw(image);
        var path = Path.Combine(Path.GetTempPath(), $"opcg-scratch-{name}-{Guid.NewGuid():N}.png");
        Cv2.ImWrite(path, image);
        return path;
    }

    private static void DrawNone(Mat image) { }

    private static void DrawThinLine(Mat image) =>
        Cv2.Line(image, new Point(40, 168), new Point(430, 176), new Scalar(8, 8, 8), 2, LineTypes.AntiAlias);

    private static void DrawGlare(Mat image) =>
        Cv2.Circle(image, new Point(240, 200), 70, new Scalar(255, 255, 255), -1);
}
