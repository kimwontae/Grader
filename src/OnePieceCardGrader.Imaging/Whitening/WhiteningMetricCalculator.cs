using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Whitening;

public sealed class WhiteningMetricCalculator
{
    public WhiteningMetricSet Compute(
        Mat candidateMask,
        Mat deltaE,
        Mat brightness,
        Mat pixelScore,
        Mat distanceFromBoundary,
        WhiteningRegion region,
        Size cardSize,
            int roiCardPixels,
            WhiteningAnalysisOptions options,
            Rect? roiOnCard = null)
    {
        var components = new List<WhiteningComponent>();
        using var labels = new Mat();
        using var stats = new Mat();
        using var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(
            candidateMask,
            labels,
            stats,
            centroids,
            PixelConnectivity.Connectivity8);

        var originX = roiOnCard?.X ?? 0;
        var originY = roiOnCard?.Y ?? 0;
        var parentW = roiOnCard is null ? candidateMask.Width : cardSize.Width;
        var parentH = roiOnCard is null ? candidateMask.Height : cardSize.Height;
        var cardArea = Math.Max(1, cardSize.Width * cardSize.Height);
        var edgeLength = region is WhiteningRegion.TopEdge or WhiteningRegion.BottomEdge
            ? cardSize.Width
            : region is WhiteningRegion.LeftEdge or WhiteningRegion.RightEdge
                ? cardSize.Height
                : (int)Math.Round(Math.Sqrt((cardSize.Width * options.CornerRoiRatio * cardSize.Width * options.CornerRoiRatio)
                                            + (cardSize.Height * options.CornerRoiRatio * cardSize.Height * options.CornerRoiRatio)));

        var projectedSet = new HashSet<int>();
        var keptPixels = 0;
        double sumDeltaE = 0;
        double maxDeltaE = 0;
        double sumBrightness = 0;
        var largestProjected = 0.0;
        var labelIndexer = labels.GetGenericIndexer<int>();
        var deltaIndexer = deltaE.GetGenericIndexer<float>();
        var brightIndexer = brightness.GetGenericIndexer<float>();
        var scoreIndexer = pixelScore.GetGenericIndexer<float>();
        var distIndexer = distanceFromBoundary.GetGenericIndexer<float>();

        for (var id = 1; id < count; id++)
        {
            var area = stats.At<int>(id, (int)ConnectedComponentsTypes.Area);
            var left = stats.At<int>(id, (int)ConnectedComponentsTypes.Left);
            var top = stats.At<int>(id, (int)ConnectedComponentsTypes.Top);
            var width = stats.At<int>(id, (int)ConnectedComponentsTypes.Width);
            var height = stats.At<int>(id, (int)ConnectedComponentsTypes.Height);
            var areaRatio = area / (double)cardArea;
            if (areaRatio < options.MinComponentAreaRatio)
            {
                continue;
            }

            double sumDe = 0, sumBr = 0, sumScore = 0, sumDist = 0, maxDe = 0;
            var xs = new HashSet<int>();
            var ys = new HashSet<int>();
            var pixels = 0;
            for (var y = top; y < top + height; y++)
            {
                for (var x = left; x < left + width; x++)
                {
                    if (labelIndexer[y, x] != id)
                    {
                        continue;
                    }

                    pixels++;
                    xs.Add(x);
                    ys.Add(y);
                    var de = deltaIndexer[y, x];
                    var br = brightIndexer[y, x];
                    sumDe += de;
                    sumBr += br;
                    sumScore += scoreIndexer[y, x];
                    sumDist += distIndexer[y, x];
                    if (de > maxDe)
                    {
                        maxDe = de;
                    }
                }
            }

            if (pixels == 0)
            {
                continue;
            }

            var projected = region switch
            {
                WhiteningRegion.TopEdge or WhiteningRegion.BottomEdge => xs.Count,
                WhiteningRegion.LeftEdge or WhiteningRegion.RightEdge => ys.Count,
                _ => EstimateArcLength(xs, ys)
            };
            var projectedRatio = projected / (double)Math.Max(1, edgeLength);
            if (projectedRatio < options.MinProjectedLengthRatio)
            {
                continue;
            }

            var meanDist = sumDist / pixels;
            var distRatio = meanDist / Math.Max(1, cardSize.Width);
            if (distRatio > options.MaxDistanceFromOuterBoundaryRatio)
            {
                continue;
            }

            if (region is WhiteningRegion.TopEdge or WhiteningRegion.BottomEdge)
            {
                foreach (var x in xs)
                {
                    projectedSet.Add(x);
                }
            }
            else if (region is WhiteningRegion.LeftEdge or WhiteningRegion.RightEdge)
            {
                foreach (var y in ys)
                {
                    projectedSet.Add(y);
                }
            }
            else
            {
                for (var i = 0; i < (int)Math.Round(projected); i++)
                {
                    projectedSet.Add((id * 10_000) + i);
                }
            }

            keptPixels += pixels;
            sumDeltaE += sumDe;
            sumBrightness += sumBr;
            if (maxDe > maxDeltaE)
            {
                maxDeltaE = maxDe;
            }

            if (projectedRatio > largestProjected)
            {
                largestProjected = projectedRatio;
            }

            components.Add(new WhiteningComponent
            {
                Id = components.Count + 1,
                AreaPixels = pixels,
                BoundingBox = NormalizedRect.FromPixels(originX + left, originY + top, width, height, parentW, parentH),
                CentroidX = centroids.At<double>(id, 0),
                CentroidY = centroids.At<double>(id, 1),
                ProjectedLength = projected,
                ProjectedLengthRatio = projectedRatio,
                MaximumWidth = region is WhiteningRegion.TopEdge or WhiteningRegion.BottomEdge ? height : width,
                MeanDeltaE = sumDe / pixels,
                MaxDeltaE = maxDe,
                MeanBrightnessIncrease = sumBr / pixels,
                MeanWhiteningPixelScore = sumScore / pixels,
                DistanceFromOuterBoundary = distRatio
            });
        }

        var lengthRatio = projectedSet.Count / (double)Math.Max(1, edgeLength);
        if (region is WhiteningRegion.TopLeftCorner or WhiteningRegion.TopRightCorner
            or WhiteningRegion.BottomLeftCorner or WhiteningRegion.BottomRightCorner)
        {
            lengthRatio = components.Sum(c => c.ProjectedLengthRatio);
        }

        return new WhiteningMetricSet(
            components,
            lengthRatio,
            keptPixels / (double)Math.Max(1, roiCardPixels),
            largestProjected,
            keptPixels == 0 ? 0 : sumDeltaE / keptPixels,
            maxDeltaE,
            keptPixels == 0 ? 0 : sumBrightness / keptPixels,
            edgeLength);
    }

    private static double EstimateArcLength(HashSet<int> xs, HashSet<int> ys)
    {
        if (xs.Count == 0 || ys.Count == 0)
        {
            return 0;
        }

        var dx = xs.Max() - xs.Min() + 1;
        var dy = ys.Max() - ys.Min() + 1;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}

public sealed record WhiteningMetricSet(
    IReadOnlyList<WhiteningComponent> Components,
    double LengthRatio,
    double AreaRatio,
    double LargestDefectRatio,
    double MeanDeltaE,
    double MaxDeltaE,
    double MeanBrightnessIncrease,
    int EdgeLengthPx);
