namespace OnePieceCardGrader.Core.Models;

public sealed class ImageQualityResult
{
    public double ResolutionScore { get; init; }
    public double FocusScore { get; init; }
    public double ExposureScore { get; init; }
    public double GlareScore { get; init; }
    public double PerspectiveScore { get; init; }
    public double CardVisibilityScore { get; init; }
    public double OverallQualityScore { get; init; }
    public bool IsAcceptable { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public double HighlightClipRatio { get; init; }
    public double ShadowClipRatio { get; init; }
    public double GlareCoverageRatio { get; init; }
}
