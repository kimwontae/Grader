namespace OnePieceCardGrader.Core.Models;

public sealed class CardDetectionResult
{
    public bool Success { get; init; }
    public QuadCorners? Corners { get; init; }
    public double Confidence { get; init; }
    public double Score { get; init; }
    public string? FailureReason { get; init; }
    public int ImageWidth { get; init; }
    public int ImageHeight { get; init; }
}
