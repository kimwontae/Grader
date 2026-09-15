using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Whitening;

internal readonly record struct RegionRoi(Rect Rect, Point Inward, bool IsCorner);

internal static class WhiteningRoi
{
    public static RegionRoi FromCard(int width, int height, WhiteningRegion region, WhiteningAnalysisOptions options)
    {
        var edgeW = Math.Max(8, (int)Math.Round(width * options.EdgeRoiRatio));
        var edgeH = Math.Max(8, (int)Math.Round(height * options.EdgeRoiRatio));
        var cornerW = Math.Max(16, (int)Math.Round(width * options.CornerRoiRatio));
        var cornerH = Math.Max(16, (int)Math.Round(height * options.CornerRoiRatio));

        return region switch
        {
            WhiteningRegion.TopEdge => new RegionRoi(new Rect(0, 0, width, edgeH), new Point(0, 1), false),
            WhiteningRegion.BottomEdge => new RegionRoi(new Rect(0, height - edgeH, width, edgeH), new Point(0, -1), false),
            WhiteningRegion.LeftEdge => new RegionRoi(new Rect(0, 0, edgeW, height), new Point(1, 0), false),
            WhiteningRegion.RightEdge => new RegionRoi(new Rect(width - edgeW, 0, edgeW, height), new Point(-1, 0), false),
            WhiteningRegion.TopLeftCorner => new RegionRoi(new Rect(0, 0, cornerW, cornerH), new Point(1, 1), true),
            WhiteningRegion.TopRightCorner => new RegionRoi(new Rect(width - cornerW, 0, cornerW, cornerH), new Point(-1, 1), true),
            WhiteningRegion.BottomLeftCorner => new RegionRoi(new Rect(0, height - cornerH, cornerW, cornerH), new Point(1, -1), true),
            _ => new RegionRoi(new Rect(width - cornerW, height - cornerH, cornerW, cornerH), new Point(-1, -1), true)
        };
    }

    public static Mat BuildCandidateZone(Size roiSize, WhiteningRegion region, int referenceStripPx)
    {
        var mask = new Mat(roiSize, MatType.CV_8UC1, Scalar.All(255));
        var strip = Math.Clamp(referenceStripPx, 2, Math.Min(roiSize.Width, roiSize.Height) - 2);
        switch (region)
        {
            case WhiteningRegion.TopEdge:
                Cv2.Rectangle(mask, new Rect(0, roiSize.Height - strip, roiSize.Width, strip), Scalar.All(0), -1);
                break;
            case WhiteningRegion.BottomEdge:
                Cv2.Rectangle(mask, new Rect(0, 0, roiSize.Width, strip), Scalar.All(0), -1);
                break;
            case WhiteningRegion.LeftEdge:
                Cv2.Rectangle(mask, new Rect(roiSize.Width - strip, 0, strip, roiSize.Height), Scalar.All(0), -1);
                break;
            case WhiteningRegion.RightEdge:
                Cv2.Rectangle(mask, new Rect(0, 0, strip, roiSize.Height), Scalar.All(0), -1);
                break;
            case WhiteningRegion.TopLeftCorner:
                Cv2.Rectangle(mask, new Rect(strip, strip, roiSize.Width - strip, roiSize.Height - strip), Scalar.All(0), -1);
                break;
            case WhiteningRegion.TopRightCorner:
                Cv2.Rectangle(mask, new Rect(0, strip, roiSize.Width - strip, roiSize.Height - strip), Scalar.All(0), -1);
                break;
            case WhiteningRegion.BottomLeftCorner:
                Cv2.Rectangle(mask, new Rect(strip, 0, roiSize.Width - strip, roiSize.Height - strip), Scalar.All(0), -1);
                break;
            default:
                Cv2.Rectangle(mask, new Rect(0, 0, roiSize.Width - strip, roiSize.Height - strip), Scalar.All(0), -1);
                break;
        }

        return mask;
    }

    public static Mat BuildReferenceZone(Size roiSize, WhiteningRegion region, int referenceStripPx)
    {
        var mask = new Mat(roiSize, MatType.CV_8UC1, Scalar.All(0));
        var strip = Math.Clamp(referenceStripPx, 2, Math.Min(roiSize.Width, roiSize.Height) - 2);
        switch (region)
        {
            case WhiteningRegion.TopEdge:
                Cv2.Rectangle(mask, new Rect(0, roiSize.Height - strip, roiSize.Width, strip), Scalar.All(255), -1);
                break;
            case WhiteningRegion.BottomEdge:
                Cv2.Rectangle(mask, new Rect(0, 0, roiSize.Width, strip), Scalar.All(255), -1);
                break;
            case WhiteningRegion.LeftEdge:
                Cv2.Rectangle(mask, new Rect(roiSize.Width - strip, 0, strip, roiSize.Height), Scalar.All(255), -1);
                break;
            case WhiteningRegion.RightEdge:
                Cv2.Rectangle(mask, new Rect(0, 0, strip, roiSize.Height), Scalar.All(255), -1);
                break;
            case WhiteningRegion.TopLeftCorner:
                Cv2.Rectangle(mask, new Rect(strip, strip, Math.Max(1, roiSize.Width - strip), Math.Max(1, roiSize.Height - strip)), Scalar.All(255), -1);
                break;
            case WhiteningRegion.TopRightCorner:
                Cv2.Rectangle(mask, new Rect(0, strip, Math.Max(1, roiSize.Width - strip), Math.Max(1, roiSize.Height - strip)), Scalar.All(255), -1);
                break;
            case WhiteningRegion.BottomLeftCorner:
                Cv2.Rectangle(mask, new Rect(strip, 0, Math.Max(1, roiSize.Width - strip), Math.Max(1, roiSize.Height - strip)), Scalar.All(255), -1);
                break;
            default:
                Cv2.Rectangle(mask, new Rect(0, 0, Math.Max(1, roiSize.Width - strip), Math.Max(1, roiSize.Height - strip)), Scalar.All(255), -1);
                break;
        }

        return mask;
    }

    public static Mat BuildOuterBoundaryMask(Size cardSize, WhiteningRegion region)
    {
        var mask = new Mat(cardSize, MatType.CV_8UC1, Scalar.All(0));
        const int thickness = 2;
        switch (region)
        {
            case WhiteningRegion.TopEdge:
            case WhiteningRegion.TopLeftCorner:
            case WhiteningRegion.TopRightCorner:
                Cv2.Rectangle(mask, new Rect(0, 0, cardSize.Width, thickness), Scalar.All(255), -1);
                break;
        }

        switch (region)
        {
            case WhiteningRegion.BottomEdge:
            case WhiteningRegion.BottomLeftCorner:
            case WhiteningRegion.BottomRightCorner:
                Cv2.Rectangle(mask, new Rect(0, cardSize.Height - thickness, cardSize.Width, thickness), Scalar.All(255), -1);
                break;
        }

        switch (region)
        {
            case WhiteningRegion.LeftEdge:
            case WhiteningRegion.TopLeftCorner:
            case WhiteningRegion.BottomLeftCorner:
                Cv2.Rectangle(mask, new Rect(0, 0, thickness, cardSize.Height), Scalar.All(255), -1);
                break;
        }

        switch (region)
        {
            case WhiteningRegion.RightEdge:
            case WhiteningRegion.TopRightCorner:
            case WhiteningRegion.BottomRightCorner:
                Cv2.Rectangle(mask, new Rect(cardSize.Width - thickness, 0, thickness, cardSize.Height), Scalar.All(255), -1);
                break;
        }

        return mask;
    }

    public static (Mat MapX, Mat MapY) BuildInwardMaps(Size roiSize, Point inward, int offsetPx, int referenceStripPx, WhiteningRegion region)
    {
        var mapX = new Mat(roiSize, MatType.CV_32FC1);
        var mapY = new Mat(roiSize, MatType.CV_32FC1);
        var indexerX = mapX.GetGenericIndexer<float>();
        var indexerY = mapY.GetGenericIndexer<float>();
        var strip = Math.Clamp(referenceStripPx, 2, Math.Min(roiSize.Width, roiSize.Height) - 1);
        var innerX = region switch
        {
            WhiteningRegion.LeftEdge or WhiteningRegion.TopLeftCorner or WhiteningRegion.BottomLeftCorner => roiSize.Width - (strip / 2f),
            WhiteningRegion.RightEdge or WhiteningRegion.TopRightCorner or WhiteningRegion.BottomRightCorner => strip / 2f,
            _ => -1f
        };
        var innerY = region switch
        {
            WhiteningRegion.TopEdge or WhiteningRegion.TopLeftCorner or WhiteningRegion.TopRightCorner => roiSize.Height - (strip / 2f),
            WhiteningRegion.BottomEdge or WhiteningRegion.BottomLeftCorner or WhiteningRegion.BottomRightCorner => strip / 2f,
            _ => -1f
        };

        for (var y = 0; y < roiSize.Height; y++)
        {
            for (var x = 0; x < roiSize.Width; x++)
            {
                var rx = innerX >= 0 ? innerX : Math.Clamp(x + (inward.X * offsetPx), 0, roiSize.Width - 1);
                var ry = innerY >= 0 ? innerY : Math.Clamp(y + (inward.Y * offsetPx), 0, roiSize.Height - 1);
                if (region is WhiteningRegion.TopEdge or WhiteningRegion.BottomEdge)
                {
                    rx = x;
                }
                else if (region is WhiteningRegion.LeftEdge or WhiteningRegion.RightEdge)
                {
                    ry = y;
                }

                indexerX[y, x] = rx;
                indexerY[y, x] = ry;
            }
        }

        return (mapX, mapY);
    }
}
