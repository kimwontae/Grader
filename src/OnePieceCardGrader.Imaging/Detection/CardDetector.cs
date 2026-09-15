using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Detection;

public sealed class CardDetector : ICardDetector
{
    private readonly AnalysisOptions _options;
    private readonly ILogger<CardDetector> _logger;

    public CardDetector(AnalysisOptions options, ILogger<CardDetector> logger)
    {
        _options = options;
        _logger = logger;
    }

    public CardDetectionResult Detect(OpenCvImage image)
    {
        var src = MatAdapter.Unwrap(image);
        _logger.LogInformation("Card detection started ({Width}x{Height})", src.Width, src.Height);

        var maxEdge = _options.Detection.ResizeMaxEdge;
        var scale = Math.Min(1.0, maxEdge / Math.Max(src.Width, src.Height));
        using var work = scale < 0.999
            ? src.Resize(new Size(), scale, scale, InterpolationFlags.Area)
            : src.Clone();

        using var gray = work.CvtColor(ColorConversionCodes.BGR2GRAY);
        var kernel = _options.Detection.GaussianKernel;
        if (kernel % 2 == 0)
        {
            kernel += 1;
        }

        using var blur = gray.GaussianBlur(new Size(kernel, kernel), 0);
        using var edges = blur.Canny(_options.Detection.CannyThreshold1, _options.Detection.CannyThreshold2);
        using var morphKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(3, 3));
        using var closed = edges.MorphologyEx(MorphTypes.Close, morphKernel);

        Cv2.FindContours(closed, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        var imageArea = work.Width * work.Height;
        var expectedAspect = _options.Card.WidthMm / _options.Card.HeightMm;
        CardCandidate? best = null;

        foreach (var contour in contours)
        {
            var area = Cv2.ContourArea(contour);
            var areaRatio = area / imageArea;
            if (areaRatio < _options.Detection.MinAreaRatio || areaRatio > _options.Detection.MaxAreaRatio)
            {
                continue;
            }

            var peri = Cv2.ArcLength(contour, true);
            var approx = Cv2.ApproxPolyDP(contour, 0.02 * peri, true);
            if (approx.Length != 4 || !Cv2.IsContourConvex(approx))
            {
                continue;
            }

            var points = approx.Select(p => new ImagePoint(p.X / scale, p.Y / scale)).ToArray();
            var quad = QuadCorners.FromUnordered(points);
            var score = ScoreCandidate(quad, src.Width, src.Height, expectedAspect, areaRatio);
            if (best is null || score > best.Score)
            {
                best = new CardCandidate(quad, score);
            }
        }

        if (best is null)
        {
            _logger.LogWarning("Card contour not found");
            return new CardDetectionResult
            {
                Success = false,
                FailureReason = "카드 사각형을 찾지 못했습니다. 네 귀퉁이를 직접 지정해 주세요.",
                ImageWidth = src.Width,
                ImageHeight = src.Height
            };
        }

        var confidence = Math.Clamp(best.Score / 100.0, 0, 1);
        var success = confidence >= _options.Detection.MinConfidence;
        _logger.LogInformation("Card contour found with score {Score:F1}", best.Score);
        return new CardDetectionResult
        {
            Success = success,
            Corners = best.Corners,
            Confidence = confidence,
            Score = best.Score,
            FailureReason = success ? null : "카드 검출 신뢰도가 낮습니다. 네 점을 확인해 주세요.",
            ImageWidth = src.Width,
            ImageHeight = src.Height
        };
    }

    private static double ScoreCandidate(QuadCorners quad, int width, int height, double expectedAspect, double areaRatio)
    {
        var points = quad.ToList();
        var polyArea = Math.Abs(Shoelace(points));
        var imageArea = (double)width * height;
        var areaScore = Math.Clamp(polyArea / imageArea * 120.0, 0, 30);

        var top = quad.TopLeft.DistanceTo(quad.TopRight);
        var bottom = quad.BottomLeft.DistanceTo(quad.BottomRight);
        var left = quad.TopLeft.DistanceTo(quad.BottomLeft);
        var right = quad.TopRight.DistanceTo(quad.BottomRight);
        var avgWidth = (top + bottom) / 2.0;
        var avgHeight = (left + right) / 2.0;
        var aspect = avgWidth / Math.Max(avgHeight, 1);
        var aspectError = Math.Abs(aspect - expectedAspect) / expectedAspect;
        var aspectScore = Math.Clamp((1.0 - aspectError) * 30.0, 0, 30);

        var widthBalance = 1.0 - Math.Min(1.0, Math.Abs(top - bottom) / Math.Max(avgWidth, 1));
        var heightBalance = 1.0 - Math.Min(1.0, Math.Abs(left - right) / Math.Max(avgHeight, 1));
        var rectangleScore = ((widthBalance + heightBalance) / 2.0) * 20.0;

        var cx = points.Average(p => p.X);
        var cy = points.Average(p => p.Y);
        var dx = (cx - (width / 2.0)) / width;
        var dy = (cy - (height / 2.0)) / height;
        var centerScore = Math.Clamp((1.0 - Math.Sqrt((dx * dx) + (dy * dy))) * 20.0, 0, 20);

        return areaScore + aspectScore + rectangleScore + centerScore + Math.Clamp(areaRatio * 10, 0, 10);
    }

    private static double Shoelace(IReadOnlyList<ImagePoint> points)
    {
        double sum = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            sum += (a.X * b.Y) - (b.X * a.Y);
        }

        return sum / 2.0;
    }

    private sealed record CardCandidate(QuadCorners Corners, double Score);
}
