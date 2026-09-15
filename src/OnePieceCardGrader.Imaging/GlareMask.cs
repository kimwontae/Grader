using OpenCvSharp;

namespace OnePieceCardGrader.Imaging;

public sealed class GlareMask : IDisposable
{
    public GlareMask(Mat mask, double coverageRatio)
    {
        Mask = mask ?? throw new ArgumentNullException(nameof(mask));
        CoverageRatio = coverageRatio;
    }

    public Mat Mask { get; }
    public double CoverageRatio { get; }

    public void Dispose() => Mask.Dispose();
}
