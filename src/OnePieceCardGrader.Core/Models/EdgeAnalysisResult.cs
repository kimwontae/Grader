using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class EdgeAnalysisResult
{
    public AnalysisStatus Status { get; init; } = AnalysisStatus.NotImplemented;
    public IReadOnlyList<EdgeResult> Edges { get; init; } = [];
    public double ConditionScore { get; init; }
    public double Confidence { get; init; }
    public int? GradeCap { get; init; }
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public string? UnavailableReason { get; init; }
}
