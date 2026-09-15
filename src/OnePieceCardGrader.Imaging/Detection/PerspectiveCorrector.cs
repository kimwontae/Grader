using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Detection;

public sealed class PerspectiveCorrector : IPerspectiveCorrector
{
    public OpenCvImage Correct(OpenCvImage image, QuadCorners corners, NormalizationOptions options)
    {
        var src = MatAdapter.Unwrap(image);
        var srcPoints = InputArray.Create(new[]
        {
            new Point2f((float)corners.TopLeft.X, (float)corners.TopLeft.Y),
            new Point2f((float)corners.TopRight.X, (float)corners.TopRight.Y),
            new Point2f((float)corners.BottomRight.X, (float)corners.BottomRight.Y),
            new Point2f((float)corners.BottomLeft.X, (float)corners.BottomLeft.Y)
        });

        var destPoints = InputArray.Create(new[]
        {
            new Point2f(0, 0),
            new Point2f(options.CanonicalWidth - 1, 0),
            new Point2f(options.CanonicalWidth - 1, options.CanonicalHeight - 1),
            new Point2f(0, options.CanonicalHeight - 1)
        });

        using var matrix = Cv2.GetPerspectiveTransform(srcPoints, destPoints);
        var warped = new Mat();
        Cv2.WarpPerspective(
            src,
            warped,
            matrix,
            new Size(options.CanonicalWidth, options.CanonicalHeight),
            InterpolationFlags.Cubic);
        return MatAdapter.Wrap(warped);
    }
}
