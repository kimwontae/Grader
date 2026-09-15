using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Core.Interfaces;

public interface IImageQualityAnalyzer
{
    ImageQualityResult Analyze(OpenCvImage image);
}

/// <summary>
/// Thin wrapper so Core does not take a hard OpenCvSharp dependency.
/// Imaging adapters construct this from a Mat.
/// </summary>
public sealed class OpenCvImage
{
    public required object NativeHandle { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
}

public interface ICardDetector
{
    CardDetectionResult Detect(OpenCvImage image);
}

public interface IPerspectiveCorrector
{
    OpenCvImage Correct(OpenCvImage image, QuadCorners corners, NormalizationOptions options);
}

public interface ICenteringAnalyzer
{
    CenteringMeasurement Analyze(
        OpenCvImage normalizedImage,
        Enums.CardSide side,
        CenteringGuide? manualGuide = null);
}

public interface ICornerAnalyzer
{
    CornerAnalysisResult Analyze(OpenCvImage image, Enums.CornerPosition position);
}

public interface IEdgeAnalyzer
{
    EdgeAnalysisResult Analyze(OpenCvImage image, Enums.EdgePosition position);
}

public interface ISurfaceAnalyzer
{
    SurfaceAnalysisResult Analyze(SurfaceImageSet images);
}

public interface ICriticalDefectEvaluator
{
    GradeCapResult Evaluate(IReadOnlyCollection<DetectedDefect> defects);
}

public interface IGradingEngine
{
    GradingResult Calculate(CardAnalysisResult analysis);
}

public interface IGradeExplanationService
{
    GradeExplanation Generate(CardAnalysisResult analysis, GradingResult result);
}

public interface ICardAnalysisPipeline
{
    Task<CardAnalysisResult> AnalyzeAsync(
        CardInput input,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken);
}

public interface ICardPreviewService
{
    Task<CardPreviewResult> CreateAsync(
        string imagePath,
        Enums.CardSide side,
        QuadCorners? manualCorners,
        CenteringGuide? manualGuide,
        CancellationToken cancellationToken);
}

public sealed class CardPreviewResult
{
    public Enums.CardSide Side { get; init; }
    public ImageQualityResult Quality { get; init; } = new();
    public CardDetectionResult Detection { get; init; } = new();
    public string OriginalPath { get; init; } = string.Empty;
    public string? NormalizedPath { get; init; }
    public CenteringMeasurement? Centering { get; init; }
    public string? FailureReason { get; init; }
}

public interface ICardTemplateProvider
{
    Task<CardTemplate?> GetTemplateAsync(string cardNumber, string language);
}

public interface IDefectDetectionModel
{
    Task<IReadOnlyList<DetectedDefect>> DetectAsync(OpenCvImage image);
}

public interface IGradingProfileProvider
{
    GradingProfile GetProfile(string name);
    IReadOnlyList<GradingProfile> GetAvailableProfiles();
}

public interface IImageStorage
{
    string RootPath { get; }
    Task<string> SaveOriginalAsync(Guid analysisId, string slotName, string sourcePath, CancellationToken cancellationToken);
    Task<string> SaveProcessedAsync(Guid analysisId, string fileName, byte[] data, CancellationToken cancellationToken);
    string GetAnalysisDirectory(Guid analysisId);
}

public interface IAnalysisRepository
{
    Task SaveAsync(CardAnalysisResult analysis, CancellationToken cancellationToken);
    Task<IReadOnlyList<DTOs.AnalysisListItemDto>> ListAsync(CancellationToken cancellationToken);
    Task<CardAnalysisResult?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task SaveActualGradeAsync(DTOs.ActualGradeFeedbackDto feedback, CancellationToken cancellationToken);
    Task ExportJsonAsync(Guid id, string destinationPath, CancellationToken cancellationToken);
}

public interface IAppSettingsStore
{
    ExpertAnalysisSettings Load();
    void Save(ExpertAnalysisSettings settings);
}

public interface IDebugImageSink
{
    bool Enabled { get; }
    void Save(string name, OpenCvImage image);
    IReadOnlyDictionary<string, string> SnapshotPaths();
}
