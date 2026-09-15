using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class CardAnalysisResult
{
    public Guid AnalysisId { get; init; } = Guid.NewGuid();
    public CardInput Input { get; init; } = new();
    public SideImageAnalysis? Front { get; init; }
    public SideImageAnalysis? Back { get; init; }
    public CenteringResult Centering { get; init; } = new() { Status = AnalysisStatus.Pending };
    public IReadOnlyList<DetectedDefect> Defects { get; init; } = [];
    public AnalysisCoverage Coverage { get; init; } = new();
    public GradingResult? Grading { get; set; }
    public GradeExplanation? Explanation { get; set; }
    public bool CanComputeGrade { get; init; }
    public string? BlockingReason { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
    public IReadOnlyDictionary<string, string> DebugImagePaths { get; init; } =
        new Dictionary<string, string>();
}
