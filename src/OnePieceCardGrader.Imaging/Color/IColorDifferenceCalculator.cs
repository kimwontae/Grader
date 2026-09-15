namespace OnePieceCardGrader.Imaging.Color;

public readonly struct LabColor
{
    public LabColor(double l, double a, double b)
    {
        L = l;
        A = a;
        B = b;
    }

    public double L { get; }
    public double A { get; }
    public double B { get; }
}

public interface IColorDifferenceCalculator
{
    double Compute(in LabColor current, in LabColor reference);
}

public sealed class Cie76ColorDifferenceCalculator : IColorDifferenceCalculator
{
    public double Compute(in LabColor current, in LabColor reference)
    {
        var dL = current.L - reference.L;
        var dA = current.A - reference.A;
        var dB = current.B - reference.B;
        return Math.Sqrt((dL * dL) + (dA * dA) + (dB * dB));
    }
}
