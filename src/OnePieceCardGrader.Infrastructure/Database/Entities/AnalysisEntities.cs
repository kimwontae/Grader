namespace OnePieceCardGrader.Infrastructure.Database.Entities;

public sealed class CardAnalysisEntity
{
    public Guid Id { get; set; }
    public string? CardName { get; set; }
    public string? CardNumber { get; set; }
    public string? SetName { get; set; }
    public string? Language { get; set; }
    public string? Rarity { get; set; }
    public string? Memo { get; set; }
    public int? PredictedGrade { get; set; }
    public int? GradeRangeMin { get; set; }
    public int? GradeRangeMax { get; set; }
    public double? Confidence { get; set; }
    public double? CenteringScore { get; set; }
    public double? CornerScore { get; set; }
    public double? EdgeScore { get; set; }
    public double? SurfaceScore { get; set; }
    public string? ThumbnailRelativePath { get; set; }
    public string? MetricsJson { get; set; }
    public string? DefectsJson { get; set; }
    public string? ResultJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ActualGradeFeedbackEntity> ActualGrades { get; set; } = [];
}

public sealed class ActualGradeFeedbackEntity
{
    public Guid Id { get; set; }
    public Guid AnalysisId { get; set; }
    public int ActualGrade { get; set; }
    public string? CertNumber { get; set; }
    public DateTimeOffset? SubmissionDate { get; set; }
    public string? UserNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
