using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed record CategoryGrade
{
    public string Name { get; init; } = string.Empty;
    public AnalysisStatus Status { get; init; }
    public double ConditionScore { get; init; }
    public int? GradeCap { get; init; }
    public double Confidence { get; init; }
    public string? UnavailableReason { get; init; }
    public IReadOnlyList<string> Notes { get; init; } = [];
}
