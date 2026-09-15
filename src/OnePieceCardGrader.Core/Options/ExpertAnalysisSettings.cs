namespace OnePieceCardGrader.Core.Options;

public sealed class ExpertAnalysisSettings
{
    public double CenteringAutoDetectSensitivity { get; set; } = 0.5;
    public double CornerDetectionSensitivity { get; set; } = 0.5;
    public double EdgeWhiteningSensitivity { get; set; } = 0.5;
    public double SurfaceScratchSensitivity { get; set; } = 0.5;
    public double GlareThreshold { get; set; } = 245;
    public double MinimumImageQuality { get; set; } = 60;
    public bool DeveloperMode { get; set; }
    public bool SaveDebugImages { get; set; }
    public string Theme { get; set; } = "Dark";
    public double CardWidthMm { get; set; } = 63.0;
    public double CardHeightMm { get; set; } = 88.0;
}
