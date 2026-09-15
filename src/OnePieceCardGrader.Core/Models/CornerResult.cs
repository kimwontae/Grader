using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class CornerResult
{
    public CornerPosition Position { get; init; }
    public CardSide Side { get; init; }
    public double SharpnessScore { get; init; }
    public double WhiteningScore { get; init; }
    public double GeometryScore { get; init; }
    public DefectSeverity Severity { get; init; }
    public double Confidence { get; init; }
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public AnalysisStatus Status { get; init; } = AnalysisStatus.NotImplemented;
    public double CombinedScore { get; init; }
    public NormalizedRect? Region { get; init; }
    public bool UsedMacroImage { get; init; }
    public bool UsedManualRegion { get; init; }
    public double WhiteningSeverity { get; init; }
    public double GeometrySeverity { get; init; }
    public WhiteningAnalysisResult? Whitening { get; init; }
    public CornerGeometryResult? Geometry { get; init; }
}
