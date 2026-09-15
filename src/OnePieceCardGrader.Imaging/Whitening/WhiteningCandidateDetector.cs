using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Whitening;

public sealed class WhiteningCandidateDetector
{
    private readonly WhiteningAnalysisOptions _options;

    public WhiteningCandidateDetector(WhiteningAnalysisOptions options)
    {
        _options = options;
    }

    public CandidateDetectionResult Detect(
        Mat labL,
        Mat labA,
        Mat labB,
        Mat hsvS,
        Mat candidateZone,
        Mat cardMaskRoi,
        Mat mapX,
        Mat mapY,
        Mat referenceZone,
        Mat? glareRoi)
    {
        SetMaps(mapX, mapY, referenceZone);
        using var brightness = new Mat();
        Cv2.Subtract(labL, GetReference(labL), brightness);
        using var dA = new Mat();
        using var dB = new Mat();
        Cv2.Subtract(labA, GetReference(labA), dA);
        Cv2.Subtract(labB, GetReference(labB), dB);

        using var dL2 = new Mat();
        using var dA2 = new Mat();
        using var dB2 = new Mat();
        Cv2.Multiply(brightness, brightness, dL2);
        Cv2.Multiply(dA, dA, dA2);
        Cv2.Multiply(dB, dB, dB2);
        using var sum = new Mat();
        Cv2.Add(dL2, dA2, sum);
        Cv2.Add(sum, dB2, sum);
        using var deltaE = new Mat();
        Cv2.Sqrt(sum, deltaE);

        using var satDecrease = new Mat();
        Cv2.Subtract(GetReference(hsvS), hsvS, satDecrease);

        using var colorOk = new Mat();
        using var brightOk = new Mat();
        Cv2.Threshold(deltaE, colorOk, _options.MinDeltaE, 255, ThresholdTypes.Binary);
        Cv2.Threshold(brightness, brightOk, _options.MinBrightnessIncrease, 255, ThresholdTypes.Binary);
        colorOk.ConvertTo(colorOk, MatType.CV_8U);
        brightOk.ConvertTo(brightOk, MatType.CV_8U);

        using var raw = new Mat();
        Cv2.BitwiseAnd(colorOk, brightOk, raw);
        Cv2.BitwiseAnd(raw, candidateZone, raw);
        Cv2.BitwiseAnd(raw, cardMaskRoi, raw);

        var glareOverlap = 0.0;
        Mat? glareRemoved = null;
        if (glareRoi is not null && !glareRoi.Empty())
        {
            using var glareBin = new Mat();
            Cv2.Threshold(glareRoi, glareBin, 0, 255, ThresholdTypes.Binary);
            using var overlap = new Mat();
            Cv2.BitwiseAnd(raw, glareBin, overlap);
            var candidateCount = Math.Max(1, Cv2.CountNonZero(raw));
            glareOverlap = Cv2.CountNonZero(overlap) / (double)candidateCount;
            using var inverted = new Mat();
            Cv2.BitwiseNot(glareBin, inverted);
            glareRemoved = new Mat();
            Cv2.BitwiseAnd(raw, inverted, glareRemoved);
        }

        var candidate = glareRemoved ?? raw.Clone();
        using var score = BuildPixelScore(deltaE, brightness, satDecrease);
        using var scoreMask = new Mat();
        Cv2.Threshold(score, scoreMask, _options.MinWhiteningPixelScore, 255, ThresholdTypes.Binary);
        scoreMask.ConvertTo(scoreMask, MatType.CV_8U);
        Cv2.BitwiseAnd(candidate, scoreMask, candidate);

        var openK = Odd(_options.MorphOpenKernel);
        var closeK = Odd(_options.MorphCloseKernel);
        using var openKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(openK, openK));
        using var closeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(closeK, closeK));
        using var opened = candidate.MorphologyEx(MorphTypes.Open, openKernel);
        var cleaned = opened.MorphologyEx(MorphTypes.Close, closeKernel);

        Cv2.MeanStdDev(labL, out _, out var refStd, referenceZone);
        return new CandidateDetectionResult(
            cleaned,
            deltaE.Clone(),
            brightness.Clone(),
            score.Clone(),
            glareOverlap,
            refStd.Val0);
    }

    private Mat? _lastReference;

    private Mat GetReference(Mat source)
    {
        _lastReference?.Dispose();
        var remapped = new Mat();
        Cv2.Remap(source, remapped, _mapX!, _mapY!, InterpolationFlags.Linear, BorderTypes.Replicate);
        var k = Odd(_options.MedianBlurKernel);
        Cv2.MedianBlur(remapped, remapped, k);
        _lastReference = remapped;
        return remapped;
    }

    private Mat? _mapX;
    private Mat? _mapY;

    public void SetMaps(Mat mapX, Mat mapY, Mat referenceZone)
    {
        _mapX = mapX;
        _mapY = mapY;
        _ = referenceZone;
    }

    private Mat BuildPixelScore(Mat deltaE, Mat brightness, Mat satDecrease)
    {
        var score = new Mat(deltaE.Size(), MatType.CV_32FC1);
        var s = score.GetGenericIndexer<float>();
        var de = deltaE.GetGenericIndexer<float>();
        var br = brightness.GetGenericIndexer<float>();
        var sat = satDecrease.GetGenericIndexer<float>();
        for (var y = 0; y < deltaE.Rows; y++)
        {
            for (var x = 0; x < deltaE.Cols; x++)
            {
                s[y, x] = (float)Core.Calculations.WhiteningScoreCalculator.ComputePixelScore(
                    de[y, x],
                    br[y, x],
                    sat[y, x],
                    _options);
            }
        }

        return score;
    }

    private static int Odd(int value)
    {
        var v = Math.Max(1, value);
        return v % 2 == 0 ? v + 1 : v;
    }
}

public sealed record CandidateDetectionResult(
    Mat CandidateMask,
    Mat DeltaE,
    Mat BrightnessIncrease,
    Mat PixelScore,
    double GlareOverlapRatio,
    double ReferenceLStdDev) : IDisposable
{
    public void Dispose()
    {
        CandidateMask.Dispose();
        DeltaE.Dispose();
        BrightnessIncrease.Dispose();
        PixelScore.Dispose();
    }
}
