namespace OnePieceCardGrader.Core.Models;

public sealed class CenteringMeasurement
{
    public double LeftPercent { get; init; }
    public double RightPercent { get; init; }
    public double TopPercent { get; init; }
    public double BottomPercent { get; init; }
    public double WorstRatio { get; init; }
    public double Confidence { get; init; }
    public double LeftMarginPx { get; init; }
    public double RightMarginPx { get; init; }
    public double TopMarginPx { get; init; }
    public double BottomMarginPx { get; init; }
    public CenteringGuide? Guide { get; init; }
}
