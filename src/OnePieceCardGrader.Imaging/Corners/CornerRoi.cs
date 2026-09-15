using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Corners;

internal static class CornerRoi
{
    public static Rect FromNormalized(int width, int height, CornerPosition position, CornerOptions options)
    {
        var roiWidth = Math.Max(24, (int)(width * options.RoiWidthPercent));
        var roiHeight = Math.Max(24, (int)(height * options.RoiHeightPercent));
        return position switch
        {
            CornerPosition.TopLeft => new Rect(0, 0, roiWidth, roiHeight),
            CornerPosition.TopRight => new Rect(width - roiWidth, 0, roiWidth, roiHeight),
            CornerPosition.BottomRight => new Rect(width - roiWidth, height - roiHeight, roiWidth, roiHeight),
            _ => new Rect(0, height - roiHeight, roiWidth, roiHeight)
        };
    }

    public static NormalizedRect ToNormalized(Rect rect, int width, int height) =>
        NormalizedRect.FromPixels(rect.X, rect.Y, rect.Width, rect.Height, width, height);
}
