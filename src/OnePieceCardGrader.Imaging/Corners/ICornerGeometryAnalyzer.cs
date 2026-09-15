using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Corners;

public interface ICornerGeometryAnalyzer
{
    CornerGeometryResult Analyze(
        Mat normalizedCard,
        CornerPosition position,
        Mat? cardMask = null,
        MeasurementContext? context = null,
        IAnalysisDebugSink? debug = null);
}
