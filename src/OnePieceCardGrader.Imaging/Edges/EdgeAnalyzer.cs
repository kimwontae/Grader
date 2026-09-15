using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Quality;
using OnePieceCardGrader.Imaging.Whitening;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Edges;

public sealed class EdgeAnalyzer : IEdgeAnalyzer
{
    private readonly IWhiteningAnalyzer _whiteningAnalyzer;
    private readonly AnalysisOptions _analysisOptions;

    public EdgeAnalyzer(IWhiteningAnalyzer whiteningAnalyzer, AnalysisOptions analysisOptions)
    {
        _whiteningAnalyzer = whiteningAnalyzer;
        _analysisOptions = analysisOptions;
    }

    public EdgeAnalysisResult Analyze(OpenCvImage image, EdgePosition position)
    {
        var src = MatAdapter.Unwrap(image);
        var glareDet = GlareDetector.Analyze(src, _analysisOptions.Quality);
        using var glare = new GlareMask(
            glareDet.Mask ?? new Mat(src.Size(), MatType.CV_8UC1, Scalar.All(0)),
            glareDet.CoverageRatio);
        var whitening = _whiteningAnalyzer.Analyze(src, WhiteningRegionMap.FromEdge(position), glare);
        var defect = DefectFactory.FromWhitening(
            whitening,
            CardSide.Front,
            position.ToString(),
            DefectType.EdgeWhitening,
            null);
        var defects = defect is null ? Array.Empty<DetectedDefect>() : new[] { defect };
        return new EdgeAnalysisResult
        {
            Status = AnalysisStatus.Completed,
            ConditionScore = whitening.ConditionScore,
            Confidence = whitening.Confidence,
            GradeCap = defects.Length == 0 ? 10 : Core.Calculations.DefectScoreMath.GradeCap(defects),
            Defects = defects,
            Edges =
            [
                new EdgeResult
                {
                    Position = position,
                    Side = CardSide.Front,
                    Score = whitening.ConditionScore,
                    WhiteningCount = whitening.DefectCount,
                    AffectedLengthPx = whitening.WhiteningLengthRatio,
                    Severity = DefectFactory.FromSeverity(whitening.Severity),
                    Confidence = whitening.Confidence / 100.0,
                    Defects = defects,
                    Status = AnalysisStatus.Completed,
                    Whitening = whitening
                }
            ]
        };
    }
}
