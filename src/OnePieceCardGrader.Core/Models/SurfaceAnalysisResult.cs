using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class SurfaceAnalysisResult
{
    public AnalysisStatus Status { get; init; } = AnalysisStatus.NotImplemented;
    public double FrontScore { get; init; }
    public double BackScore { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public string? UnavailableReason { get; init; }
}
