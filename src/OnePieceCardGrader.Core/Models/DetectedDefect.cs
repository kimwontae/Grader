using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class DetectedDefect
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DefectType Type { get; init; }
    public DefectSeverity Severity { get; init; }
    public CardSide Side { get; init; }
    public NormalizedRect? Region { get; init; }
    public double Confidence { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Impact { get; init; } = string.Empty;
    public string? ZoomImagePath { get; set; }
    public Dictionary<string, double> Metrics { get; init; } = new();
    public bool AutoDetected { get; init; } = true;
    public DefectReviewStatus ReviewStatus { get; set; } = DefectReviewStatus.AutoDetected;
    public int? GradeCap { get; init; }
}
