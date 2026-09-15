using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Surface;

public interface IScratchAnalyzer
{
    ScratchAnalysisResult Analyze(
        SurfaceImageSet imageSet,
        CardSide side,
        MeasurementContext? context = null,
        IAnalysisDebugSink? debug = null);
}
