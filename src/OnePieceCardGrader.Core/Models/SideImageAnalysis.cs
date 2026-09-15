using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class SideImageAnalysis
{
    public CardSide Side { get; init; }
    public ImageQualityResult Quality { get; init; } = new();
    public CardDetectionResult Detection { get; init; } = new();
    public string? OriginalPath { get; init; }
    public string? NormalizedPath { get; init; }
    public string? OverlayPath { get; init; }
    public int NormalizedWidth { get; init; }
    public int NormalizedHeight { get; init; }
}
