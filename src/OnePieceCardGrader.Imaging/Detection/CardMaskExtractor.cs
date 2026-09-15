using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Detection;

public static class CardMaskExtractor
{
    public static Mat Extract(Mat bgr, double threshold = 12)
    {
        using var gray = bgr.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var binary = new Mat();
        Cv2.Threshold(gray, binary, threshold, 255, ThresholdTypes.Binary);
        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        using var closed = binary.MorphologyEx(MorphTypes.Close, kernel);

        var labels = new Mat();
        var stats = new Mat();
        var centroids = new Mat();
        var count = Cv2.ConnectedComponentsWithStats(closed, labels, stats, centroids, PixelConnectivity.Connectivity8);
        try
        {
            var mask = new Mat(bgr.Size(), MatType.CV_8UC1, Scalar.All(0));
            if (count <= 1)
            {
                closed.CopyTo(mask);
                return mask;
            }

            var best = 1;
            var bestArea = 0;
            for (var i = 1; i < count; i++)
            {
                var area = stats.At<int>(i, (int)ConnectedComponentsTypes.Area);
                if (area > bestArea)
                {
                    bestArea = area;
                    best = i;
                }
            }

            using var cmp = new Mat();
            Cv2.Compare(labels, best, cmp, CmpType.EQ);
            cmp.CopyTo(mask);
            using var fillKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(7, 7));
            Cv2.MorphologyEx(mask, mask, MorphTypes.Close, fillKernel);
            return mask;
        }
        finally
        {
            labels.Dispose();
            stats.Dispose();
            centroids.Dispose();
        }
    }
}
