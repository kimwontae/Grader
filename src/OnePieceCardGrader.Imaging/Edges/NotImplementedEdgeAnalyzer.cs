using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.Imaging.Edges;

public sealed class NotImplementedEdgeAnalyzer : IEdgeAnalyzer
{
    public EdgeAnalysisResult Analyze(OpenCvImage image, EdgePosition position) =>
        new()
        {
            Status = AnalysisStatus.NotImplemented,
            UnavailableReason = "Edge 분석은 아직 구현되지 않았습니다."
        };
}
