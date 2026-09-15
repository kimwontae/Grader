using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class SurfaceAnalysisResult
{
    public AnalysisStatus Status { get; init; } = AnalysisStatus.NotImplemented;
    public double FrontScore { get; init; }
    public double BackScore { get; init; }
    public double ConditionScore { get; init; }
    public int? GradeCap { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public string? UnavailableReason { get; init; }
    public bool UsedAngledLight { get; init; }
    public bool UsedMacroFallback { get; init; }
    public ScratchAnalysisResult? FrontScratch { get; init; }
    public ScratchAnalysisResult? BackScratch { get; init; }
}
