using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Preprocessing;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Pipeline;

public sealed class CardPreviewService : ICardPreviewService
{
    private readonly IImageQualityAnalyzer _qualityAnalyzer;
    private readonly ICardDetector _cardDetector;
    private readonly IPerspectiveCorrector _perspectiveCorrector;
    private readonly ICenteringAnalyzer _centeringAnalyzer;
    private readonly AnalysisOptions _options;

    public CardPreviewService(
        IImageQualityAnalyzer qualityAnalyzer,
        ICardDetector cardDetector,
        IPerspectiveCorrector perspectiveCorrector,
        ICenteringAnalyzer centeringAnalyzer,
        AnalysisOptions options)
    {
        _qualityAnalyzer = qualityAnalyzer;
        _cardDetector = cardDetector;
        _perspectiveCorrector = perspectiveCorrector;
        _centeringAnalyzer = centeringAnalyzer;
        _options = options;
    }

    public Task<CardPreviewResult> CreateAsync(
        string imagePath,
        CardSide side,
        QuadCorners? manualCorners,
        CenteringGuide? manualGuide,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = ExifOrientation.LoadOriented(imagePath);
        var quality = _qualityAnalyzer.Analyze(MatAdapter.Wrap(source));
        var detection = manualCorners is not null
            ? new CardDetectionResult
            {
                Success = true,
                Corners = manualCorners,
                Confidence = 0.99,
                Score = 99,
                ImageWidth = source.Width,
                ImageHeight = source.Height
            }
            : _cardDetector.Detect(MatAdapter.Wrap(source));

        string? normalizedPath = null;
        CenteringMeasurement? centering = null;
        string? failure = null;

        if (!detection.Success || detection.Corners is null)
        {
            failure = detection.FailureReason ?? "카드 영역을 찾지 못했습니다.";
        }
        else
        {
            var corrected = _perspectiveCorrector.Correct(MatAdapter.Wrap(source), detection.Corners, _options.Normalization);
            var mat = MatAdapter.Unwrap(corrected);
            try
            {
                var directory = Path.Combine(Path.GetTempPath(), "OnePieceCardGrader", "preview");
                Directory.CreateDirectory(directory);
                normalizedPath = Path.Combine(directory, $"{side.ToString().ToLowerInvariant()}_{Guid.NewGuid():N}.jpg");
                Cv2.ImWrite(normalizedPath, mat);
                centering = _centeringAnalyzer.Analyze(MatAdapter.Wrap(mat), side, manualGuide);
            }
            finally
            {
                mat.Dispose();
            }
        }

        return Task.FromResult(new CardPreviewResult
        {
            Side = side,
            Quality = quality,
            Detection = detection,
            OriginalPath = imagePath,
            NormalizedPath = normalizedPath,
            Centering = centering,
            FailureReason = failure
        });
    }
}
