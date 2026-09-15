using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Whitening;

public interface IWhiteningAnalyzer
{
    WhiteningAnalysisResult Analyze(
        Mat normalizedCard,
        WhiteningRegion region,
        GlareMask? glareMask = null,
        MeasurementContext? context = null,
        IAnalysisDebugSink? debug = null);
}
