using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class EdgeResult
{
    public EdgePosition Position { get; init; }
    public CardSide Side { get; init; }
    public double Score { get; init; }
    public double MeanContourDeviation { get; init; }
    public double MaxContourDeviation { get; init; }
    public double DeviationStdDev { get; init; }
    public int WhiteningCount { get; init; }
    public double AffectedLengthPx { get; init; }
    public DefectSeverity Severity { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public AnalysisStatus Status { get; init; } = AnalysisStatus.NotImplemented;
}
