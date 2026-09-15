using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;

namespace OnePieceCardGrader.Core.Models;

public sealed class CornerGeometryResult : IAnalysisResult
{
    public CornerPosition Position { get; init; }
    public double Severity { get; init; }
    public double ConditionScore { get; init; }
    public double Confidence { get; init; }
    public double MeanContourDeviation { get; init; }
    public double MaxContourDeviation { get; init; }
    public double MissingAreaRatio { get; init; }
    public double ExcessAreaRatio { get; init; }
    public double RadiusDeviation { get; init; }
    public double MeasuredRadiusRatio { get; init; }
    public double ExpectedRadiusRatio { get; init; }
    public double Roughness { get; init; }
    public double CurvatureMean { get; init; }
    public double CurvatureStdDev { get; init; }
    public double HighFrequencyCurvatureEnergy { get; init; }
    public double Continuity { get; init; }
    public CornerGeometryType Type { get; init; }
}
