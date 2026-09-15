using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class GradingResult
{
    public int PredictedGrade { get; init; }
    public int GradeRangeMin { get; init; }
    public int GradeRangeMax { get; init; }
    public double AnalysisConfidence { get; init; }
    public CategoryGrade Centering { get; init; } = new() { Name = "Centering" };
    public CategoryGrade Corners { get; init; } = new() { Name = "Corners", Status = AnalysisStatus.NotImplemented };
    public CategoryGrade Edges { get; init; } = new() { Name = "Edges", Status = AnalysisStatus.NotImplemented };
    public CategoryGrade Surface { get; init; } = new() { Name = "Surface", Status = AnalysisStatus.NotImplemented };
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public IReadOnlyList<string> GradeLimitReasons { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
    public bool IsBorderline { get; init; }
    public string? PrimaryTenLimiter { get; init; }
}
