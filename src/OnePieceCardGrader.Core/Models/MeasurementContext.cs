namespace OnePieceCardGrader.Core.Models;

public sealed class MeasurementContext
{
    public double ImageQuality { get; init; } = 70;
    public double Focus { get; init; } = 70;
    public double Exposure { get; init; } = 70;
    public double CardDetectionConfidence { get; init; } = 0.8;
    public bool HasAngledLight { get; init; }
}
