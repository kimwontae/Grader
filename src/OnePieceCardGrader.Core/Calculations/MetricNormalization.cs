namespace OnePieceCardGrader.Core.Calculations;

public static class MetricNormalization
{
    public static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    public static double Normalize(double value, double min, double max)
    {
        if (max <= min)
        {
            return value >= max ? 1.0 : 0.0;
        }

        return Clamp01((value - min) / (max - min));
    }

    public static double NormalizeWithReference(double value, double reference)
    {
        if (reference <= 0)
        {
            return value > 0 ? 1.0 : 0.0;
        }

        return Clamp01(value / reference);
    }

    public static double CombineComplement(double firstSeverity, double secondSeverity)
    {
        var a = Clamp01(firstSeverity);
        var b = Clamp01(secondSeverity);
        return 1.0 - ((1.0 - a) * (1.0 - b));
    }

    public static double WeightedAverage(params (double Value, double Weight)[] parts)
    {
        var weightSum = parts.Sum(p => p.Weight);
        if (weightSum <= 0)
        {
            return 0;
        }

        return parts.Sum(p => p.Value * p.Weight) / weightSum;
    }
}
