using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class CornerAnalysisResult
{
    public AnalysisStatus Status { get; init; } = AnalysisStatus.NotImplemented;
    public IReadOnlyList<CornerResult> Corners { get; init; } = [];
    public double ConditionScore { get; init; }
    public double Confidence { get; init; }
    public int? GradeCap { get; init; }
    public string? UnavailableReason { get; init; }
}
