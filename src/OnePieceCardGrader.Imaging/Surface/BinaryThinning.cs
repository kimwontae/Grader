using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Surface;

internal static class BinaryThinning
{
    public static Mat Thin(Mat binary)
    {
        var src = binary.Clone();
        if (src.Type() != MatType.CV_8UC1)
        {
            src.ConvertTo(src, MatType.CV_8UC1);
        }

        Cv2.Threshold(src, src, 0, 1, ThresholdTypes.Binary);
        var changed = true;
        while (changed)
        {
            changed = ZhangSuenIteration(src, step2: false) | ZhangSuenIteration(src, step2: true);
        }

        Cv2.Threshold(src, src, 0, 255, ThresholdTypes.Binary);
        return src;
    }

    private static bool ZhangSuenIteration(Mat img, bool step2)
    {
        var markers = new List<Point>();
        var indexer = img.GetGenericIndexer<byte>();
        var h = img.Rows;
        var w = img.Cols;
        for (var y = 1; y < h - 1; y++)
        {
            for (var x = 1; x < w - 1; x++)
            {
                if (indexer[y, x] == 0)
                {
                    continue;
                }

                var p2 = indexer[y - 1, x];
                var p3 = indexer[y - 1, x + 1];
                var p4 = indexer[y, x + 1];
                var p5 = indexer[y + 1, x + 1];
                var p6 = indexer[y + 1, x];
                var p7 = indexer[y + 1, x - 1];
                var p8 = indexer[y, x - 1];
                var p9 = indexer[y - 1, x - 1];
                var neighbors = p2 + p3 + p4 + p5 + p6 + p7 + p8 + p9;
                if (neighbors is < 2 or > 6)
                {
                    continue;
                }

                var transitions =
                    (p2 == 0 && p3 == 1 ? 1 : 0)
                    + (p3 == 0 && p4 == 1 ? 1 : 0)
                    + (p4 == 0 && p5 == 1 ? 1 : 0)
                    + (p5 == 0 && p6 == 1 ? 1 : 0)
                    + (p6 == 0 && p7 == 1 ? 1 : 0)
                    + (p7 == 0 && p8 == 1 ? 1 : 0)
                    + (p8 == 0 && p9 == 1 ? 1 : 0)
                    + (p9 == 0 && p2 == 1 ? 1 : 0);
                if (transitions != 1)
                {
                    continue;
                }

                if (!step2)
                {
                    if (p2 * p4 * p6 != 0 || p4 * p6 * p8 != 0)
                    {
                        continue;
                    }
                }
                else if (p2 * p4 * p8 != 0 || p2 * p6 * p8 != 0)
                {
                    continue;
                }

                markers.Add(new Point(x, y));
            }
        }

        foreach (var point in markers)
        {
            indexer[point.Y, point.X] = 0;
        }

        return markers.Count > 0;
    }
}
