namespace OnePieceCardGrader.Core.Models;

public sealed class AnalysisProgress
{
    public string Stage { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public double Percent { get; init; }
}
