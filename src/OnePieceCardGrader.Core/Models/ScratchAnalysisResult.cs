using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;

namespace OnePieceCardGrader.Core.Models;

public sealed class AlignmentResult
{
    public int MatchCount { get; init; }
    public int InlierCount { get; init; }
    public double InlierRatio { get; init; }
    public double ReprojectionError { get; init; }
    public double Confidence { get; init; }
    public bool UsedAngledComparison { get; init; }
}

public sealed class ScratchCandidate
{
    public int Id { get; init; }
    public NormalizedRect BoundingBox { get; init; } = new();
    public double LengthRatio { get; init; }
    public double WidthRatio { get; init; }
    public double AspectRatio { get; init; }
    public double LocalContrast { get; init; }
    public double GradientStrength { get; init; }
    public double Straightness { get; init; }
    public double Curvature { get; init; }
    public double LightingResponse { get; init; }
    public double GlareOverlap { get; init; }
    public double WidthConsistency { get; init; }
    public double DetectionConfidence { get; init; }
    public double Severity { get; init; }
    public ScratchCandidateType Type { get; init; }
}

public sealed class ScratchAnalysisResult : IAnalysisResult
{
    public double Severity { get; init; }
    public double ConditionScore { get; init; }
    public double Confidence { get; init; }
    public int CandidateCount { get; init; }
    public double WorstScratchSeverity { get; init; }
    public double TotalAffectedAreaRatio { get; init; }
    public IReadOnlyList<ScratchCandidate> Candidates { get; init; } = [];
    public AlignmentResult? Alignment { get; init; }
}
