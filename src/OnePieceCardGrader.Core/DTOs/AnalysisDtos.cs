namespace OnePieceCardGrader.Core.DTOs;

public sealed class AnalysisListItemDto
{
    public Guid Id { get; init; }
    public string? CardName { get; init; }
    public string? CardNumber { get; init; }
    public int? PredictedGrade { get; init; }
    public int? GradeRangeMin { get; init; }
    public int? GradeRangeMax { get; init; }
    public double? Confidence { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public int? ActualPsaGrade { get; init; }
    public string? ThumbnailPath { get; init; }
}

public sealed class ActualGradeFeedbackDto
{
    public Guid AnalysisId { get; init; }
    public int ActualGrade { get; init; }
    public string? CertNumber { get; init; }
    public DateTimeOffset? SubmissionDate { get; init; }
    public string? UserNote { get; init; }
}

public sealed class AnalysisExportDto
{
    public object? Card { get; init; }
    public object? Images { get; init; }
    public object? Measurements { get; init; }
    public object? Defects { get; init; }
    public object? Prediction { get; init; }
    public object? ActualGrade { get; init; }
}
