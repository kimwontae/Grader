namespace OnePieceCardGrader.Core.Options;

public sealed class ImageAnalysisOptions
{
    public WhiteningAnalysisOptions Whitening { get; set; } = new();
    public CornerGeometryAnalysisOptions CornerGeometry { get; set; } = new();
    public ScratchAnalysisOptions Scratch { get; set; } = new();
}

public sealed class WhiteningAnalysisOptions
{
    public double EdgeRoiRatio { get; set; } = 0.05;
    public double CornerRoiRatio { get; set; } = 0.14;
    public double ReferenceInnerOffsetRatio { get; set; } = 0.015;
    public int LocalReferenceMinPx { get; set; } = 5;
    public int LocalReferenceMaxPx { get; set; } = 15;

    public double MinDeltaE { get; set; } = 12.0;
    public double StrongDeltaE { get; set; } = 30.0;
    public double MinBrightnessIncrease { get; set; } = 12.0;
    public double StrongBrightnessIncrease { get; set; } = 45.0;
    public double MinSaturationDecrease { get; set; } = 8.0;
    public double StrongSaturationDecrease { get; set; } = 30.0;

    public double MinComponentAreaRatio { get; set; } = 0.00001;
    public double MinProjectedLengthRatio { get; set; } = 0.001;
    public double MaxDistanceFromOuterBoundaryRatio { get; set; } = 0.02;

    public int MorphOpenKernel { get; set; } = 3;
    public int MorphCloseKernel { get; set; } = 3;
    public int MedianBlurKernel { get; set; } = 5;

    public double SevereLengthRatio { get; set; } = 0.08;
    public double SevereAreaRatio { get; set; } = 0.04;
    public double SevereLargestDefectRatio { get; set; } = 0.05;

    public double LengthWeight { get; set; } = 0.45;
    public double AreaWeight { get; set; } = 0.25;
    public double ContrastWeight { get; set; } = 0.20;
    public double LargestDefectWeight { get; set; } = 0.10;

    public double ColorDifferenceScoreWeight { get; set; } = 0.45;
    public double BrightnessScoreWeight { get; set; } = 0.40;
    public double SaturationScoreWeight { get; set; } = 0.15;
    public double MinWhiteningPixelScore { get; set; } = 0.35;

    public double ConditionPenaltyScale { get; set; } = 45.0;
    public double LargestDefectPenaltyThreshold { get; set; } = 0.7;
    public double LargestDefectExtraPenalty { get; set; } = 8.0;

    public double ImageQualityWeight { get; set; } = 0.30;
    public double ReferenceStabilityWeight { get; set; } = 0.25;
    public double GlareQualityWeight { get; set; } = 0.20;
    public double DetectionQualityWeight { get; set; } = 0.25;
}

public sealed class CornerGeometryAnalysisOptions
{
    public double CornerRoiRatio { get; set; } = 0.14;
    public int ContourSampleCount { get; set; } = 128;
    public double MaxNormalDeviationRatio { get; set; } = 0.003;
    public double RadiusDeviationReference { get; set; } = 0.02;
    public double MissingAreaReference { get; set; } = 0.05;
    public double RoughnessReference { get; set; } = 0.005;
    public int SmoothingWindow { get; set; } = 5;
    public double StableEdgeInsetRatio { get; set; } = 0.35;
    public double DefaultCornerRadiusRatio { get; set; } = 0.018;
    public double CardMaskThreshold { get; set; } = 12.0;

    public double MissingAreaWeight { get; set; } = 0.35;
    public double DeviationWeight { get; set; } = 0.30;
    public double RadiusWeight { get; set; } = 0.20;
    public double RoughnessWeight { get; set; } = 0.15;

    public double ConditionPenaltyScale { get; set; } = 50.0;
    public double CombinedPenaltyScale { get; set; } = 50.0;
    public double WorstCornerWeight { get; set; } = 0.50;
    public double SecondWorstCornerWeight { get; set; } = 0.20;
    public double AverageCornerWeight { get; set; } = 0.30;

    public double ChippingMissingAreaThreshold { get; set; } = 0.45;
    public double RoundingRadiusThreshold { get; set; } = 0.45;
    public double RoughnessTypeThreshold { get; set; } = 0.50;
}

public sealed class ScratchAnalysisOptions
{
    public double MinScratchLengthRatio { get; set; } = 0.005;
    public double MinAspectRatio { get; set; } = 4.0;
    public double MaxWidthRatio { get; set; } = 0.01;
    public double MinLocalContrast { get; set; } = 8.0;
    public double MinGradientResponse { get; set; } = 12.0;
    public int MorphKernel { get; set; } = 3;
    public double GlareExclusionThreshold { get; set; } = 0.65;
    public int AlignmentMinMatches { get; set; } = 15;
    public double CandidateScoreThreshold { get; set; } = 0.35;
    public int DownscaleMaxEdge { get; set; } = 1600;
    public int TophatKernel { get; set; } = 17;
    public int BlackHatKernel { get; set; } = 17;
    public double MinInlierRatio { get; set; } = 0.35;
    public double MaxReprojectionError { get; set; } = 4.0;
    public double LoweRatio { get; set; } = 0.75;
    public double MinStraightness { get; set; } = 0.35;
    public double StrongLocalContrast { get; set; } = 28.0;
    public double StrongGradientResponse { get; set; } = 40.0;
    public int OrbFeatureCount { get; set; } = 1800;

    public double LengthWeight { get; set; } = 0.20;
    public double ThinnessWeight { get; set; } = 0.15;
    public double ContrastWeight { get; set; } = 0.20;
    public double GradientWeight { get; set; } = 0.15;
    public double LightingWeight { get; set; } = 0.20;
    public double ShapeWeight { get; set; } = 0.10;

    public double ScratchLengthSeverityWeight { get; set; } = 0.40;
    public double ScratchWidthSeverityWeight { get; set; } = 0.15;
    public double ScratchContrastSeverityWeight { get; set; } = 0.25;
    public double ScratchVisualImpactWeight { get; set; } = 0.20;

    public double WorstScratchWeight { get; set; } = 0.55;
    public double SecondWorstScratchWeight { get; set; } = 0.15;
    public double AffectedAreaWeight { get; set; } = 0.15;
    public double CountWeight { get; set; } = 0.15;

    public double ConditionPenaltyScale { get; set; } = 45.0;
    public double SevereLengthRatio { get; set; } = 0.08;
    public double SevereWidthRatio { get; set; } = 0.006;
    public double SevereAffectedAreaRatio { get; set; } = 0.01;
    public double SevereCount { get; set; } = 8.0;

    public double NormalImageQualityWeight { get; set; } = 0.20;
    public double AngledImageQualityWeight { get; set; } = 0.15;
    public double AlignmentConfidenceWeight { get; set; } = 0.20;
    public double GlareCoverageWeight { get; set; } = 0.15;
    public double ShapeConfidenceWeight { get; set; } = 0.15;
    public double LightingAvailabilityWeight { get; set; } = 0.15;
}
