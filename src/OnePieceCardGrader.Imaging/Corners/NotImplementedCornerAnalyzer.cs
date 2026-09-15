using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Imaging.Corners;

public sealed class NotImplementedCornerAnalyzer : ICornerAnalyzer
{
    public CornerAnalysisResult Analyze(OpenCvImage image, CornerPosition position) =>
        new()
        {
            Status = AnalysisStatus.NotImplemented,
            UnavailableReason = "Corner 분석은 아직 구현되지 않았습니다."
        };
}
