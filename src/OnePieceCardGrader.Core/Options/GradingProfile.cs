namespace OnePieceCardGrader.Core.Options;

public sealed class GradingProfile
{
    public string Name { get; set; } = "PSA";
    public string Version { get; set; } = "1.0";
    public GradingWeights Weights { get; set; } = new();
    public Dictionary<string, CenteringThreshold> Centering { get; set; } = new();
    public Dictionary<string, int> DefectRules { get; set; } = new();
    public Dictionary<string, double> GradeThresholds { get; set; } = new();
    public double BorderlineDistance { get; set; } = 1.0;
}

public sealed class GradingWeights
{
    public double Centering { get; set; } = 0.20;
    public double Corners { get; set; } = 0.30;
    public double Edges { get; set; } = 0.20;
    public double Surface { get; set; } = 0.30;
}

public sealed class CenteringThreshold
{
    public double FrontMax { get; set; }
    public double BackMax { get; set; }
}
