using OnePieceCardGrader.Core.Interfaces;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging;

public static class MatAdapter
{
    public static OpenCvImage Wrap(Mat mat)
    {
        ArgumentNullException.ThrowIfNull(mat);
        return new OpenCvImage
        {
            NativeHandle = mat,
            Width = mat.Width,
            Height = mat.Height
        };
    }

    public static Mat Unwrap(OpenCvImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.NativeHandle is Mat mat && !mat.IsDisposed)
        {
            return mat;
        }

        throw new InvalidOperationException("OpenCvImage does not contain a live OpenCvSharp Mat.");
    }
}
