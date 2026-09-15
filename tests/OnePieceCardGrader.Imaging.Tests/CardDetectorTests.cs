using Microsoft.Extensions.Logging.Abstractions;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging;
using OnePieceCardGrader.Imaging.Detection;
using OnePieceCardGrader.Imaging.Quality;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Tests;

public sealed class CardDetectorTests
{
    [Fact]
    public void Detector_ShouldFindHighContrastRectangle()
    {
        using var image = new Mat(1200, 900, MatType.CV_8UC3, new Scalar(20, 20, 20));
        Cv2.Rectangle(image, new Rect(180, 80, 540, 980), new Scalar(40, 80, 210), -1);
        Cv2.Rectangle(image, new Rect(200, 110, 500, 920), new Scalar(30, 40, 60), -1);

        var options = new AnalysisOptions();
        var detector = new CardDetector(options, NullLogger<CardDetector>.Instance);
        var result = detector.Detect(MatAdapter.Wrap(image));
        Assert.True(result.Success);
        Assert.NotNull(result.Corners);
    }

    [Fact]
    public void Perspective_ShouldProduceCanonicalSize()
    {
        using var image = new Mat(800, 600, MatType.CV_8UC3, new Scalar(10, 10, 10));
        var corners = new Core.Models.QuadCorners
        {
            TopLeft = new Core.Models.ImagePoint(50, 40),
            TopRight = new Core.Models.ImagePoint(540, 60),
            BottomRight = new Core.Models.ImagePoint(560, 760),
            BottomLeft = new Core.Models.ImagePoint(40, 740)
        };
        var corrector = new PerspectiveCorrector();
        var corrected = corrector.Correct(MatAdapter.Wrap(image), corners, new NormalizationOptions());
        using var mat = MatAdapter.Unwrap(corrected);
        Assert.Equal(1260, mat.Width);
        Assert.Equal(1760, mat.Height);
    }
}
