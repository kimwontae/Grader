namespace OnePieceCardGrader.Core.Options;

public sealed class AnalysisOptions
{
    public CardGeometryOptions Card { get; set; } = new();
    public NormalizationOptions Normalization { get; set; } = new();
    public QualityOptions Quality { get; set; } = new();
    public DetectionOptions Detection { get; set; } = new();
    public CenteringOptions Centering { get; set; } = new();
    public CornerOptions Corners { get; set; } = new();
    public EdgeOptions Edges { get; set; } = new();
    public SurfaceOptions Surface { get; set; } = new();
}

public sealed class CardGeometryOptions
{
    public double WidthMm { get; set; } = 63.0;
    public double HeightMm { get; set; } = 88.0;
}

public sealed class NormalizationOptions
{
    public int CanonicalWidth { get; set; } = 1260;
    public int CanonicalHeight { get; set; } = 1760;
}

public sealed class QualityOptions
{
    public int MinShortSidePx { get; set; } = 1500;
    public double FocusExcellent { get; set; } = 90;
    public double FocusAcceptable { get; set; } = 75;
    public double FocusCaution { get; set; } = 60;
    public double FocusReferenceVariance { get; set; } = 180;
    public double HighlightClipThreshold { get; set; } = 250;
    public double ShadowClipThreshold { get; set; } = 5;
    public double GlareValueThreshold { get; set; } = 245;
    public double GlareSaturationThreshold { get; set; } = 40;
    public double MaxGlareCoverage { get; set; } = 0.18;
    public double MinOverallQuality { get; set; } = 60;
    public double PerspectiveSkewLimit { get; set; } = 0.22;
}

public sealed class DetectionOptions
{
    public double ResizeMaxEdge { get; set; } = 1400;
    public int GaussianKernel { get; set; } = 5;
    public double CannyThreshold1 { get; set; } = 50;
    public double CannyThreshold2 { get; set; } = 150;
    public double MinAreaRatio { get; set; } = 0.08;
    public double MaxAreaRatio { get; set; } = 0.98;
    public double AspectRatioTolerance { get; set; } = 0.28;
    public double MinConfidence { get; set; } = 0.35;
}

public sealed class CenteringOptions
{
    public double BorderSearchPercent { get; set; } = 0.18;
    public double MinPrintInsetPercent { get; set; } = 0.012;
    public double LowConfidenceThreshold { get; set; } = 0.55;
    public double PeakProminence { get; set; } = 1.35;
}

public sealed class CornerOptions
{
    public double RoiWidthPercent { get; set; } = 0.12;
    public double RoiHeightPercent { get; set; } = 0.12;
    public double WhiteningDelta { get; set; } = 18;
    public double MinWhiteningAreaRatio { get; set; } = 0.0035;
    public double Sensitivity { get; set; } = 0.5;
}

public sealed class EdgeOptions
{
    public double RoiPercent { get; set; } = 0.05;
}

public sealed class SurfaceOptions
{
    public double ScratchSensitivity { get; set; } = 0.5;
    public double GlareExcludeThreshold { get; set; } = 0.6;
    public double MinElongation { get; set; } = 4.2;
    public int MinScratchLengthPx { get; set; } = 16;
    public int TophatKernel { get; set; } = 17;
    public double MaxGlareOverlap { get; set; } = 0.4;
}
