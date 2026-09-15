using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class SurfaceImageSet
{
    public string? NormalFrontPath { get; init; }
    public string? NormalBackPath { get; init; }
    public string? AngledFrontPath { get; init; }
    public string? AngledBackPath { get; init; }
}
