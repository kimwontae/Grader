using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Imaging.Whitening;

internal static class WhiteningRegionMap
{
    public static WhiteningRegion FromEdge(EdgePosition position) => position switch
    {
        EdgePosition.Top => WhiteningRegion.TopEdge,
        EdgePosition.Right => WhiteningRegion.RightEdge,
        EdgePosition.Bottom => WhiteningRegion.BottomEdge,
        _ => WhiteningRegion.LeftEdge
    };

    public static WhiteningRegion FromCorner(CornerPosition position) => position switch
    {
        CornerPosition.TopLeft => WhiteningRegion.TopLeftCorner,
        CornerPosition.TopRight => WhiteningRegion.TopRightCorner,
        CornerPosition.BottomRight => WhiteningRegion.BottomRightCorner,
        _ => WhiteningRegion.BottomLeftCorner
    };
}
