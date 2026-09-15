using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Imaging.Surface;

public sealed class NotImplementedSurfaceAnalyzer : ISurfaceAnalyzer
{
    public SurfaceAnalysisResult Analyze(SurfaceImageSet images) =>
        new()
        {
            Status = AnalysisStatus.NotImplemented,
            UnavailableReason = "Surface 분석은 아직 구현되지 않았습니다."
        };
}
