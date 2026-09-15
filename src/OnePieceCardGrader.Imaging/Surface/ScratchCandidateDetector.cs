using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Surface;

public sealed class ScratchCandidateDetector
{
    private readonly ScratchAnalysisOptions _options;

    public ScratchCandidateDetector(ScratchAnalysisOptions options)
    {
        _options = options;
    }

    public ScratchMaskSet Detect(Mat gray, Mat glareMask)
    {
        using var scharrX = gray.Scharr(MatType.CV_32F, 1, 0);
        using var scharrY = gray.Scharr(MatType.CV_32F, 0, 1);
        using var grad = new Mat();
        Cv2.Magnitude(scharrX, scharrY, grad);
        using var grad8 = new Mat();
        Cv2.Normalize(grad, grad8, 0, 255, NormTypes.MinMax);
        grad8.ConvertTo(grad8, MatType.CV_8U);
        using var gradientCandidate = new Mat();
        Cv2.Threshold(grad8, gradientCandidate, _options.MinGradientResponse, 255, ThresholdTypes.Binary);

        var k = Odd(_options.TophatKernel);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(k, k));
        using var tophat = gray.MorphologyEx(MorphTypes.TopHat, kernel);
        using var blackhat = gray.MorphologyEx(MorphTypes.BlackHat, kernel);
        using var topBin = new Mat();
        using var blackBin = new Mat();
        var morphThreshold = Math.Max(6, _options.MinLocalContrast * 0.6);
        Cv2.Threshold(tophat, topBin, morphThreshold, 255, ThresholdTypes.Binary);
        Cv2.Threshold(blackhat, blackBin, morphThreshold, 255, ThresholdTypes.Binary);

        using var canny = gray.Canny(_options.MinGradientResponse, _options.MinGradientResponse * 2.2);
        using var morphCandidates = new Mat();
        Cv2.BitwiseOr(topBin, blackBin, morphCandidates);
        using var cannyMasked = new Mat();
        Cv2.BitwiseAnd(canny, morphCandidates, cannyMasked);
        using var gradMasked = new Mat();
        Cv2.BitwiseAnd(gradientCandidate, morphCandidates, gradMasked);
        var combined = new Mat();
        Cv2.BitwiseOr(morphCandidates, cannyMasked, combined);
        Cv2.BitwiseOr(combined, gradMasked, combined);

        using var invertedGlare = new Mat();
        Cv2.BitwiseNot(glareMask, invertedGlare);
        Cv2.BitwiseAnd(combined, invertedGlare, combined);

        var morph = Odd(_options.MorphKernel);
        using var openK = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 1));
        using var openK2 = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 2));
        using var openedH = combined.MorphologyEx(MorphTypes.Open, openK);
        using var openedV = combined.MorphologyEx(MorphTypes.Open, openK2);
        using var opened = new Mat();
        Cv2.BitwiseOr(openedH, openedV, opened);
        using var hKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(15, 1));
        using var vKernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(1, 15));
        using var hOpen = opened.MorphologyEx(MorphTypes.Open, hKernel);
        using var vOpen = opened.MorphologyEx(MorphTypes.Open, vKernel);
        using var closeK = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(morph + 2, morph + 2));
        using var closed = opened.MorphologyEx(MorphTypes.Close, closeK);
        var cleaned = new Mat();
        Cv2.BitwiseOr(closed, hOpen, cleaned);
        Cv2.BitwiseOr(cleaned, vOpen, cleaned);
        return new ScratchMaskSet(cleaned, grad8.Clone(), tophat.Clone(), blackhat.Clone());
    }

    private static int Odd(int value)
    {
        var v = Math.Max(1, value);
        return v % 2 == 0 ? v + 1 : v;
    }
}

public sealed record ScratchMaskSet(Mat CandidateMask, Mat GradientMap, Mat TopHat, Mat BlackHat) : IDisposable
{
    public void Dispose()
    {
        CandidateMask.Dispose();
        GradientMap.Dispose();
        TopHat.Dispose();
        BlackHat.Dispose();
    }
}

public sealed class ScratchMetricCalculator
{
    private readonly ScratchAnalysisOptions _options;

    public ScratchMetricCalculator(ScratchAnalysisOptions options)
    {
        _options = options;
    }

    public IReadOnlyList<ScratchComponentMetrics> Measure(
        Mat candidateMask,
        Mat gray,
        Mat gradient,
        Mat glareMask,
        Mat? lightingDiff,
        Size imageSize)
    {
        var results = new List<ScratchComponentMetrics>();
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(candidateMask, labels, stats, centroids);
        var diag = Math.Sqrt((imageSize.Width * imageSize.Width) + (imageSize.Height * imageSize.Height));

        for (var id = 1; id < count; id++)
        {
            var area = stats.At<int>(id, (int)ConnectedComponentsTypes.Area);
            var left = stats.At<int>(id, (int)ConnectedComponentsTypes.Left);
            var top = stats.At<int>(id, (int)ConnectedComponentsTypes.Top);
            var width = stats.At<int>(id, (int)ConnectedComponentsTypes.Width);
            var height = stats.At<int>(id, (int)ConnectedComponentsTypes.Height);
            if (area < 8 || area > imageSize.Width * imageSize.Height * 0.02)
            {
                continue;
            }

            var boxAspect = Math.Max(width, height) / (double)Math.Max(1, Math.Min(width, height));
            if (boxAspect < 2.2 && area > 80)
            {
                continue;
            }

            var pad = 2;
            var roi = new Rect(
                Math.Max(0, left - pad),
                Math.Max(0, top - pad),
                Math.Min(candidateMask.Width - Math.Max(0, left - pad), width + (pad * 2)),
                Math.Min(candidateMask.Height - Math.Max(0, top - pad), height + (pad * 2)));
            using var crop = new Mat(candidateMask, roi);
            using var cropLabels = new Mat(labels, roi);
            using var componentRoi = new Mat(crop.Size(), MatType.CV_8UC1, Scalar.All(0));
            var cropLabelIndexer = cropLabels.GetGenericIndexer<int>();
            var roiIndexer = componentRoi.GetGenericIndexer<byte>();
            for (var y = 0; y < crop.Rows; y++)
            {
                for (var x = 0; x < crop.Cols; x++)
                {
                    if (cropLabelIndexer[y, x] == id)
                    {
                        roiIndexer[y, x] = 255;
                    }
                }
            }

            using var skeletonRoi = BinaryThinning.Thin(componentRoi);
            var skeletonLength = Math.Max(1, Cv2.CountNonZero(skeletonRoi));
            var lengthRatio = skeletonLength / Math.Max(1.0, diag);
            var meanWidth = area / (double)skeletonLength;
            var widthRatio = meanWidth / Math.Max(1.0, Math.Max(imageSize.Width, imageSize.Height));
            var aspect = skeletonLength / Math.Max(meanWidth, 0.5);
            if (lengthRatio < _options.MinScratchLengthRatio
                || aspect < _options.MinAspectRatio
                || widthRatio > _options.MaxWidthRatio)
            {
                continue;
            }

            var endpoints = FindEndpoints(skeletonRoi);
            var euclid = endpoints is { A: not null, B: not null }
                ? Distance(endpoints.Value.A!.Value, endpoints.Value.B!.Value)
                : Math.Sqrt((width * width) + (height * height));
            var straightness = Math.Clamp(euclid / skeletonLength, 0, 1);
            if (straightness < _options.MinStraightness)
            {
                continue;
            }

            using var component = new Mat(candidateMask.Size(), MatType.CV_8UC1, Scalar.All(0));
            using var dest = new Mat(component, roi);
            componentRoi.CopyTo(dest);
            using var skeleton = new Mat(candidateMask.Size(), MatType.CV_8UC1, Scalar.All(0));
            using var skelDest = new Mat(skeleton, roi);
            skeletonRoi.CopyTo(skelDest);

            var localContrast = MeasureLocalContrast(gray, component, skeleton);
            if (localContrast < _options.MinLocalContrast)
            {
                continue;
            }

            var gradientStrength = Cv2.Mean(gradient, component).Val0;
            if (gradientStrength < _options.MinGradientResponse)
            {
                continue;
            }

            using var glareOverlapMat = new Mat();
            Cv2.BitwiseAnd(component, glareMask, glareOverlapMat);
            var glareOverlap = Cv2.CountNonZero(glareOverlapMat) / (double)Math.Max(1, area);
            if (glareOverlap > _options.GlareExclusionThreshold)
            {
                continue;
            }

            var lighting = lightingDiff is null ? 0.0 : Cv2.Mean(lightingDiff, component).Val0 / 40.0;
            lighting = MetricNormalization.Clamp01(lighting);
            var widthConsistency = MeasureWidthConsistency(component, skeletonLength);
            var curvature = 1.0 - straightness;

            var lengthScore = MetricNormalization.NormalizeWithReference(lengthRatio, _options.SevereLengthRatio);
            var thinnessScore = MetricNormalization.Clamp01(1.0 - MetricNormalization.NormalizeWithReference(widthRatio, _options.MaxWidthRatio));
            var contrastScore = MetricNormalization.Normalize(localContrast, _options.MinLocalContrast, _options.StrongLocalContrast);
            var gradientScore = MetricNormalization.Normalize(gradientStrength, _options.MinGradientResponse, _options.StrongGradientResponse);
            var lightingScore = lightingDiff is null ? 0.35 : lighting;
            var shapeScore = MetricNormalization.Normalize(straightness, _options.MinStraightness, 1.0);
            var candidateScore = ScratchScoreCalculator.ComputeCandidateScore(
                lengthScore, thinnessScore, contrastScore, gradientScore, lightingScore, shapeScore, glareOverlap, _options);
            if (candidateScore < _options.CandidateScoreThreshold)
            {
                continue;
            }

            var lengthSeverity = lengthScore;
            var widthSeverity = MetricNormalization.NormalizeWithReference(widthRatio, _options.SevereWidthRatio);
            var contrastSeverity = contrastScore;
            var visualImpact = MetricNormalization.Clamp01((lengthScore * 0.6) + (contrastScore * 0.4));
            var severity = ScratchScoreCalculator.ComputeScratchSeverity(
                lengthSeverity, widthSeverity, contrastSeverity, visualImpact, _options);

            results.Add(new ScratchComponentMetrics(
                id,
                new Rect(left, top, width, height),
                lengthRatio,
                widthRatio,
                aspect,
                localContrast,
                gradientStrength,
                straightness,
                curvature,
                lighting,
                glareOverlap,
                widthConsistency,
                candidateScore,
                severity,
                area / (double)Math.Max(1, imageSize.Width * imageSize.Height)));
        }

        return results;
    }

    private static (Point? A, Point? B)? FindEndpoints(Mat skeleton)
    {
        Point? first = null;
        Point? second = null;
        var indexer = skeleton.GetGenericIndexer<byte>();
        for (var y = 1; y < skeleton.Rows - 1; y++)
        {
            for (var x = 1; x < skeleton.Cols - 1; x++)
            {
                if (indexer[y, x] == 0)
                {
                    continue;
                }

                var n = 0;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0)
                        {
                            continue;
                        }

                        if (indexer[y + dy, x + dx] != 0)
                        {
                            n++;
                        }
                    }
                }

                if (n == 1)
                {
                    if (first is null)
                    {
                        first = new Point(x, y);
                    }
                    else
                    {
                        second = new Point(x, y);
                    }
                }
            }
        }

        return (first, second);
    }

    private static double MeasureLocalContrast(Mat gray, Mat component, Mat skeleton)
    {
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
        using var dilated = new Mat();
        Cv2.Dilate(skeleton, dilated, kernel);
        using var neighborhood = new Mat();
        Cv2.Subtract(dilated, component, neighborhood);
        var inner = Cv2.Mean(gray, component).Val0;
        var outer = Cv2.Mean(gray, neighborhood).Val0;
        return Math.Abs(inner - outer);
    }

    private static double MeasureWidthConsistency(Mat component, int skeletonLength)
    {
        var area = Cv2.CountNonZero(component);
        var mean = area / (double)Math.Max(1, skeletonLength);
        return MetricNormalization.Clamp01(1.0 / (1.0 + Math.Abs(mean - 1.5)));
    }

    private static double Distance(Point a, Point b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public sealed record ScratchComponentMetrics(
    int Id,
    Rect BoundingBox,
    double LengthRatio,
    double WidthRatio,
    double AspectRatio,
    double LocalContrast,
    double GradientStrength,
    double Straightness,
    double Curvature,
    double LightingResponse,
    double GlareOverlap,
    double WidthConsistency,
    double DetectionConfidence,
    double Severity,
    double AreaRatio);
