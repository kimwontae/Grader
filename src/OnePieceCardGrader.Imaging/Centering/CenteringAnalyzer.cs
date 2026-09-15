using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Centering;

public sealed class CenteringAnalyzer : ICenteringAnalyzer
{
    private readonly AnalysisOptions _options;
    private readonly PrintBoundaryDetector _boundaryDetector;
    private readonly ILogger<CenteringAnalyzer> _logger;

    public CenteringAnalyzer(AnalysisOptions options, ILogger<CenteringAnalyzer> logger)
    {
        _options = options;
        _logger = logger;
        _boundaryDetector = new PrintBoundaryDetector(options);
    }

    public CenteringMeasurement Analyze(OpenCvImage normalizedImage, CardSide side, CenteringGuide? manualGuide = null)
    {
        var src = MatAdapter.Unwrap(normalizedImage);
        if (manualGuide is not null)
        {
            _logger.LogInformation("Manual centering used for {Side}", side);
            return CenteringMath.FromGuide(manualGuide, src.Width, src.Height, confidence: 0.92);
        }

        var detection = _boundaryDetector.Detect(src);
        var guide = detection.Guide;
        var measurement = CenteringMath.FromGuide(guide, src.Width, src.Height, detection.Confidence);
        if (detection.Confidence < _options.Centering.LowConfidenceThreshold)
        {
            _logger.LogWarning("Centering auto detection failed or is low-confidence for {Side}", side);
        }
        else
        {
            _logger.LogInformation(
                "Centering auto detection for {Side}: L/R {Left:F1}/{Right:F1}, T/B {Top:F1}/{Bottom:F1}",
                side,
                measurement.LeftPercent,
                measurement.RightPercent,
                measurement.TopPercent,
                measurement.BottomPercent);
        }

        return measurement;
    }
}
