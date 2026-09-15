using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Quality;

public sealed class ImageQualityAnalyzer : IImageQualityAnalyzer
{
    private readonly AnalysisOptions _options;

    public ImageQualityAnalyzer(AnalysisOptions options)
    {
        _options = options;
    }

    public ImageQualityResult Analyze(OpenCvImage image)
    {
        var src = MatAdapter.Unwrap(image);
        using var gray = src.CvtColor(ColorConversionCodes.BGR2GRAY);
        var glare = GlareDetector.Analyze(src, _options.Quality);

        var resolutionScore = ScoreResolution(src.Width, src.Height);
        var focusScore = ScoreFocus(gray);
        var exposure = ScoreExposure(gray);
        var glareScore = ScoreGlare(glare.CoverageRatio);
        var geometry = EstimateGeometry(src);
        var overall = WeightedOverall(resolutionScore, focusScore, exposure.Score, glareScore, geometry.Perspective, geometry.Visibility);
        var acceptable = overall >= _options.Quality.MinOverallQuality
                         && focusScore >= _options.Quality.FocusCaution
                         && geometry.Visibility >= 40
                         && glare.CoverageRatio <= _options.Quality.MaxGlareCoverage;

        var warnings = new List<string>();
        if (resolutionScore < 70)
        {
            warnings.Add($"해상도가 낮습니다. 짧은 변이 {_options.Quality.MinShortSidePx}px 이상인 사진을 권장합니다.");
        }

        if (focusScore < _options.Quality.FocusCaution)
        {
            warnings.Add("초점이 흐려 재촬영을 권장합니다.");
        }
        else if (focusScore < _options.Quality.FocusAcceptable)
        {
            warnings.Add("초점이 다소 흐립니다. 미세 결함 검출 정확도가 낮아질 수 있습니다.");
        }

        if (exposure.HighlightClipRatio > 0.04)
        {
            warnings.Add("하이라이트 클리핑이 감지되었습니다.");
        }

        if (exposure.ShadowClipRatio > 0.08)
        {
            warnings.Add("어두운 영역이 많아 노출이 부족할 수 있습니다.");
        }

        if (glare.CoverageRatio > 0.08)
        {
            warnings.Add($"반사광이 표면의 {glare.CoverageRatio:P0}를 가리고 있습니다.");
        }

        if (geometry.Perspective < 55)
        {
            warnings.Add("촬영 각도가 비스듬합니다. 가능하면 수직으로 다시 촬영해 주세요.");
        }

        if (geometry.Visibility < 50)
        {
            warnings.Add("카드 전체가 보이지 않거나 배경 대비가 약합니다.");
        }

        return new ImageQualityResult
        {
            ResolutionScore = resolutionScore,
            FocusScore = focusScore,
            ExposureScore = exposure.Score,
            GlareScore = glareScore,
            PerspectiveScore = geometry.Perspective,
            CardVisibilityScore = geometry.Visibility,
            OverallQualityScore = overall,
            IsAcceptable = acceptable,
            Warnings = warnings,
            HighlightClipRatio = exposure.HighlightClipRatio,
            ShadowClipRatio = exposure.ShadowClipRatio,
            GlareCoverageRatio = glare.CoverageRatio
        };
    }

    private double ScoreResolution(int width, int height)
    {
        var shortSide = Math.Min(width, height);
        var ratio = shortSide / (double)_options.Quality.MinShortSidePx;
        return Math.Clamp(ratio * 100.0, 0, 100);
    }

    private double ScoreFocus(Mat gray)
    {
        var scale = 1000.0 / Math.Max(Math.Min(gray.Width, gray.Height), 1);
        scale = Math.Clamp(scale, 0.25, 2.0);
        using var resized = gray.Resize(new Size(), scale, scale, InterpolationFlags.Area);
        using var laplacian = resized.Laplacian(MatType.CV_64F);
        Cv2.MeanStdDev(laplacian, out _, out var std);
        var variance = std.Val0 * std.Val0;
        var normalized = variance / Math.Max(_options.Quality.FocusReferenceVariance, 1);
        return Math.Clamp(Math.Log(normalized + 1) / Math.Log(2) * 70.0, 0, 100);
    }

    private (double Score, double HighlightClipRatio, double ShadowClipRatio) ScoreExposure(Mat gray)
    {
        using var hist = new Mat();
        Cv2.CalcHist([gray], [0], null, hist, 1, [256], [new Rangef(0, 256)]);
        var total = gray.Rows * gray.Cols;
        var shadow = 0.0;
        var highlight = 0.0;
        var weighted = 0.0;
        for (var i = 0; i < 256; i++)
        {
            var count = hist.Get<float>(i);
            weighted += i * count;
            if (i <= _options.Quality.ShadowClipThreshold)
            {
                shadow += count;
            }

            if (i >= _options.Quality.HighlightClipThreshold)
            {
                highlight += count;
            }
        }

        var mean = weighted / Math.Max(total, 1);
        var highlightRatio = highlight / Math.Max(total, 1);
        var shadowRatio = shadow / Math.Max(total, 1);
        var meanScore = 100.0 - (Math.Abs(mean - 127.0) / 127.0 * 100.0);
        var clipPenalty = (highlightRatio * 180.0) + (shadowRatio * 90.0);
        var score = Math.Clamp(meanScore - clipPenalty, 0, 100);
        return (score, highlightRatio, shadowRatio);
    }

    private static double ScoreGlare(double coverage) =>
        Math.Clamp(100.0 - (coverage * 350.0), 0, 100);

    private static (double Perspective, double Visibility) EstimateGeometry(Mat src)
    {
        using var gray = src.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var blur = gray.GaussianBlur(new Size(5, 5), 0);
        using var edges = blur.Canny(50, 150);
        Cv2.FindContours(edges, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0)
        {
            return (50, 30);
        }

        var imageArea = src.Width * src.Height;
        var best = contours.OrderByDescending(c => Cv2.ContourArea(c)).FirstOrDefault();
        if (best is null || best.Length == 0)
        {
            return (50, 30);
        }

        var area = Cv2.ContourArea(best);
        var visibility = Math.Clamp(area / imageArea * 140.0, 0, 100);
        var rect = Cv2.MinAreaRect(best);
        var rectArea = rect.Size.Width * rect.Size.Height;
        var rectangularity = rectArea <= 1 ? 0 : area / rectArea;
        var perspective = Math.Clamp(rectangularity * 100.0, 0, 100);
        return (perspective, visibility);
    }

    private static double WeightedOverall(
        double resolution,
        double focus,
        double exposure,
        double glare,
        double perspective,
        double visibility) =>
        Math.Clamp(
            (resolution * 0.18) +
            (focus * 0.28) +
            (exposure * 0.16) +
            (glare * 0.16) +
            (perspective * 0.10) +
            (visibility * 0.12),
            0,
            100);
}
