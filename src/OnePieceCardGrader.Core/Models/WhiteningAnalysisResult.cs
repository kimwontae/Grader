using OnePieceCardGrader.Core.Interfaces;

namespace OnePieceCardGrader.Core.Models;

public sealed class WhiteningComponent
{
    public int Id { get; init; }
    public int AreaPixels { get; init; }
    public NormalizedRect BoundingBox { get; init; } = new();
    public double CentroidX { get; init; }
    public double CentroidY { get; init; }
    public double ProjectedLength { get; init; }
    public double ProjectedLengthRatio { get; init; }
    public double MaximumWidth { get; init; }
    public double MeanDeltaE { get; init; }
    public double MaxDeltaE { get; init; }
    public double MeanBrightnessIncrease { get; init; }
    public double MeanWhiteningPixelScore { get; init; }
    public double DistanceFromOuterBoundary { get; init; }
}

public sealed class WhiteningAnalysisResult : IAnalysisResult
{
    public double Severity { get; init; }
    public double ConditionScore { get; init; }
    public double Confidence { get; init; }
    public double WhiteningLengthRatio { get; init; }
    public double WhiteningAreaRatio { get; init; }
    public double LargestDefectRatio { get; init; }
    public double MeanDeltaE { get; init; }
    public double MaxDeltaE { get; init; }
    public double MeanBrightnessIncrease { get; init; }
    public int DefectCount { get; init; }
    public double GlareOverlapRatio { get; init; }
    public double ReferenceColorVariance { get; init; }
    public IReadOnlyList<WhiteningComponent> Components { get; init; } = [];
}
