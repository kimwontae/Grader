using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class CenteringResult
{
    public AnalysisStatus Status { get; init; }
    public CenteringMeasurement? Front { get; init; }
    public CenteringMeasurement? Back { get; init; }
    public int EstimatedGradeCap { get; init; } = 10;
    public IReadOnlyList<string> Notes { get; init; } = [];
    public CenteringMode ModeUsed { get; init; } = CenteringMode.Auto;
}
