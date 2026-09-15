namespace OnePieceCardGrader.Core.Models;

public sealed class AnalysisCoverage
{
    public double Centering { get; init; }
    public double Corners { get; init; }
    public double Edges { get; init; }
    public double Surface { get; init; }
    public double Overall { get; init; }
    public bool HasFront { get; init; }
    public bool HasBack { get; init; }
    public bool HasAngledSurface { get; init; }
    public int MacroCornerCount { get; init; }
    public int MacroEdgeCount { get; init; }
}
