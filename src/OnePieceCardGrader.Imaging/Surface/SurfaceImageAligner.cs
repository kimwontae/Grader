using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Surface;

public sealed class SurfaceImageAligner
{
    public AlignmentOutcome Align(Mat referenceBgr, Mat movingBgr, ScratchAnalysisOptions options)
    {
        using var refGray = referenceBgr.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var movGray = movingBgr.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var akaze = AKAZE.Create();
        using var refDesc = new Mat();
        using var movDesc = new Mat();
        akaze.DetectAndCompute(refGray, null, out var refKeys, refDesc);
        akaze.DetectAndCompute(movGray, null, out var movKeys, movDesc);
        if (refDesc.Empty() || movDesc.Empty() || refKeys.Length < 8 || movKeys.Length < 8)
        {
            return AlignmentOutcome.Failed(refKeys.Length, 0);
        }

        using var matcher = new BFMatcher(NormTypes.Hamming, false);
        var knn = matcher.KnnMatch(refDesc, movDesc, 2);
        var good = knn
            .Where(m => m.Length == 2 && m[0].Distance < options.LoweRatio * m[1].Distance)
            .Select(m => m[0])
            .ToArray();
        if (good.Length < options.AlignmentMinMatches)
        {
            return AlignmentOutcome.Failed(good.Length, 0);
        }

        var src = good.Select(m => movKeys[m.TrainIdx].Pt).ToArray();
        var dst = good.Select(m => refKeys[m.QueryIdx].Pt).ToArray();
        using var inlierMask = new Mat();
        using var homography = Cv2.FindHomography(
            InputArray.Create(src),
            InputArray.Create(dst),
            HomographyMethods.Ransac,
            3.0,
            inlierMask);
        if (homography.Empty())
        {
            return AlignmentOutcome.Failed(good.Length, 0);
        }

        var inliers = 0;
        double errorSum = 0;
        var maskIndexer = inlierMask.GetGenericIndexer<byte>();
        for (var i = 0; i < good.Length; i++)
        {
            if (maskIndexer[i, 0] == 0)
            {
                continue;
            }

            inliers++;
            var warped = WarpPoint(src[i], homography);
            var dx = warped.X - dst[i].X;
            var dy = warped.Y - dst[i].Y;
            errorSum += Math.Sqrt((dx * dx) + (dy * dy));
        }

        var inlierRatio = inliers / (double)Math.Max(1, good.Length);
        var reproj = inliers == 0 ? 999 : errorSum / inliers;
        var used = inliers >= options.AlignmentMinMatches
                   && inlierRatio >= options.MinInlierRatio
                   && reproj <= options.MaxReprojectionError;
        var confidence = used
            ? Math.Clamp((inlierRatio * 0.6) + ((1.0 - Math.Min(reproj / options.MaxReprojectionError, 1.0)) * 0.4), 0, 1)
            : Math.Clamp(inlierRatio * 0.4, 0, 0.4);

        Mat? warpedBgr = null;
        if (used)
        {
            warpedBgr = new Mat();
            Cv2.WarpPerspective(movingBgr, warpedBgr, homography, referenceBgr.Size());
        }

        return new AlignmentOutcome(
            new AlignmentResult
            {
                MatchCount = good.Length,
                InlierCount = inliers,
                InlierRatio = inlierRatio,
                ReprojectionError = reproj,
                Confidence = confidence,
                UsedAngledComparison = used
            },
            warpedBgr);
    }

    private static Point2f WarpPoint(Point2f point, Mat homography)
    {
        var warped = Cv2.PerspectiveTransform([point], homography);
        return warped.Length == 0 ? point : warped[0];
    }
}

public sealed record AlignmentOutcome(AlignmentResult Result, Mat? WarpedBgr) : IDisposable
{
    public static AlignmentOutcome Failed(int matches, int inliers) =>
        new(
            new AlignmentResult
            {
                MatchCount = matches,
                InlierCount = inliers,
                UsedAngledComparison = false,
                Confidence = 0
            },
            null);

    public void Dispose() => WarpedBgr?.Dispose();
}
