using OnePieceCardGrader.Core.Enums;

namespace OnePieceCardGrader.Core.Models;

public sealed class GradeCapResult
{
    public int MaxGrade { get; init; } = 10;
    public IReadOnlyList<string> Reasons { get; init; } = [];
    public IReadOnlyList<DetectedDefect> CriticalDefects { get; init; } = [];
}
