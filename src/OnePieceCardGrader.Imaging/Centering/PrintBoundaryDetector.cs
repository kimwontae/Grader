using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Centering;

internal sealed class PrintBoundaryDetector
{
    private readonly AnalysisOptions _options;

    public PrintBoundaryDetector(AnalysisOptions options)
    {
        _options = options;
    }

    public PrintBoundaryDetection Detect(Mat normalized)
    {
        using var gray = normalized.CvtColor(ColorConversionCodes.BGR2GRAY);
        using var blur = gray.GaussianBlur(new Size(3, 3), 0);
        using var sobelX = blur.Sobel(MatType.CV_32F, 1, 0, 3);
        using var sobelY = blur.Sobel(MatType.CV_32F, 0, 1, 3);
        using var absX = sobelX.Abs();
        using var absY = sobelY.Abs();

        var left = FindInset(absX, horizontal: true, fromStart: true);
        var right = FindInset(absX, horizontal: true, fromStart: false);
        var top = FindInset(absY, horizontal: false, fromStart: true);
        var bottom = FindInset(absY, horizontal: false, fromStart: false);

        var guide = new CenteringGuide
        {
            PrintLeft = left.Normalized,
            PrintRight = 1.0 - right.Normalized,
            PrintTop = top.Normalized,
            PrintBottom = 1.0 - bottom.Normalized
        };

        if (guide.PrintRight - guide.PrintLeft < 0.55 || guide.PrintBottom - guide.PrintTop < 0.55)
        {
            return new PrintBoundaryDetection(CenteringGuide.CreateDefault(), 0.25);
        }

        var confidence = (left.Confidence + right.Confidence + top.Confidence + bottom.Confidence) / 4.0;
        return new PrintBoundaryDetection(guide, Math.Clamp(confidence, 0, 1));
    }

    private SidePeak FindInset(Mat energy, bool horizontal, bool fromStart)
    {
        var length = horizontal ? energy.Width : energy.Height;
        var search = Math.Max(8, (int)(length * _options.Centering.BorderSearchPercent));
        var minInset = Math.Max(2, (int)(length * _options.Centering.MinPrintInsetPercent));
        var projection = new double[search];

        for (var i = 0; i < search; i++)
        {
            var index = fromStart ? i : length - 1 - i;
            projection[i] = horizontal
                ? ColumnMean(energy, index)
                : RowMean(energy, index);
        }

        var mean = projection.Average();
        var peakIndex = minInset;
        var peakValue = double.MinValue;
        for (var i = minInset; i < search; i++)
        {
            if (projection[i] > peakValue)
            {
                peakValue = projection[i];
                peakIndex = i;
            }
        }

        var prominence = mean <= 1e-6 ? 1 : peakValue / mean;
        var confidence = prominence >= _options.Centering.PeakProminence
            ? Math.Clamp((prominence - 1.0) / 2.0, 0.35, 0.95)
            : 0.30;

        if (prominence < _options.Centering.PeakProminence)
        {
            peakIndex = (int)(length * 0.04);
            confidence = 0.28;
        }

        return new SidePeak(peakIndex / (double)length, confidence);
    }

    private static double ColumnMean(Mat energy, int x)
    {
        var margin = (int)(energy.Height * 0.12);
        var sum = 0.0;
        var count = 0;
        for (var y = margin; y < energy.Height - margin; y++)
        {
            sum += energy.Get<float>(y, x);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static double RowMean(Mat energy, int y)
    {
        var margin = (int)(energy.Width * 0.12);
        var sum = 0.0;
        var count = 0;
        for (var x = margin; x < energy.Width - margin; x++)
        {
            sum += energy.Get<float>(y, x);
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private readonly record struct SidePeak(double Normalized, double Confidence);
}

internal sealed record PrintBoundaryDetection(CenteringGuide Guide, double Confidence);
