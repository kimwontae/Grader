namespace OnePieceCardGrader.Core.Models;

public sealed class GradeExplanation
{
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> LimitReasons { get; init; } = [];
    public string? PrimaryTenLimiter { get; init; }
    public IReadOnlyList<string> PositiveNotes { get; init; } = [];
}
