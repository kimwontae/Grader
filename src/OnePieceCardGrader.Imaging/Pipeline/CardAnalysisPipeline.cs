using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging.Preprocessing;
using OnePieceCardGrader.Imaging.Visualization;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Pipeline;

public sealed class CardAnalysisPipeline : ICardAnalysisPipeline
{
    private readonly IImageQualityAnalyzer _qualityAnalyzer;
    private readonly ICardDetector _cardDetector;
    private readonly IPerspectiveCorrector _perspectiveCorrector;
    private readonly ICenteringAnalyzer _centeringAnalyzer;
    private readonly ICriticalDefectEvaluator _criticalEvaluator;
    private readonly IGradingEngine _gradingEngine;
    private readonly IGradeExplanationService _explanationService;
    private readonly IImageStorage _imageStorage;
    private readonly IAppSettingsStore _settingsStore;
    private readonly AnalysisOptions _baseOptions;
    private readonly IGradingProfileProvider _profileProvider;
    private readonly ILogger<CardAnalysisPipeline> _logger;

    public CardAnalysisPipeline(
        IImageQualityAnalyzer qualityAnalyzer,
        ICardDetector cardDetector,
        IPerspectiveCorrector perspectiveCorrector,
        ICenteringAnalyzer centeringAnalyzer,
        ICriticalDefectEvaluator criticalEvaluator,
        IGradingEngine gradingEngine,
        IGradeExplanationService explanationService,
        IImageStorage imageStorage,
        IAppSettingsStore settingsStore,
        AnalysisOptions baseOptions,
        IGradingProfileProvider profileProvider,
        ILogger<CardAnalysisPipeline> logger)
    {
        _qualityAnalyzer = qualityAnalyzer;
        _cardDetector = cardDetector;
        _perspectiveCorrector = perspectiveCorrector;
        _centeringAnalyzer = centeringAnalyzer;
        _criticalEvaluator = criticalEvaluator;
        _gradingEngine = gradingEngine;
        _explanationService = explanationService;
        _imageStorage = imageStorage;
        _settingsStore = settingsStore;
        _baseOptions = baseOptions;
        _profileProvider = profileProvider;
        _logger = logger;
    }

    public async Task<CardAnalysisResult> AnalyzeAsync(
        CardInput input,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.FrontImagePath) || !File.Exists(input.FrontImagePath))
        {
            throw new InvalidOperationException("Front 전체 사진은 필수입니다.");
        }

        var analysisId = Guid.NewGuid();
        var settings = _settingsStore.Load();
        var options = MergeOptions(settings);
        var debug = new FileDebugImageSink(
            Path.Combine(_imageStorage.GetAnalysisDirectory(analysisId), "debug"),
            settings.DeveloperMode && settings.SaveDebugImages);

        Report(progress, "이미지 품질 확인", "Front 이미지를 불러오는 중", 5);
        var frontOriginalPath = await _imageStorage.SaveOriginalAsync(analysisId, "front_original", input.FrontImagePath, cancellationToken);
        using var frontMat = ExifOrientation.LoadOriented(frontOriginalPath);
        debug.Save("01_original_front", MatAdapter.Wrap(frontMat));

        cancellationToken.ThrowIfCancellationRequested();
        var frontSide = AnalyzeSide(
            analysisId,
            CardSide.Front,
            frontMat,
            frontOriginalPath,
            input.FrontManualCorners,
            options,
            debug,
            progress,
            10,
            cancellationToken);

        SideImageAnalysis? backSide = null;
        Mat? backMat = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(input.BackImagePath) && File.Exists(input.BackImagePath))
            {
                Report(progress, "이미지 품질 확인", "Back 이미지를 불러오는 중", 30);
                var backOriginalPath = await _imageStorage.SaveOriginalAsync(analysisId, "back_original", input.BackImagePath, cancellationToken);
                backMat = ExifOrientation.LoadOriented(backOriginalPath);
                debug.Save("01_original_back", MatAdapter.Wrap(backMat));
                backSide = AnalyzeSide(
                    analysisId,
                    CardSide.Back,
                    backMat,
                    backOriginalPath,
                    input.BackManualCorners,
                    options,
                    debug,
                    progress,
                    35,
                    cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, "센터링 분석", "Front/Back 센터링을 계산하는 중", 55);

            var warnings = new List<string>();
            warnings.AddRange(frontSide.Quality.Warnings);
            if (backSide is not null)
            {
                warnings.AddRange(backSide.Quality.Warnings);
            }

            var canCompute = frontSide.Detection.Success && frontSide.NormalizedPath is not null;
            string? blocking = null;
            if (!frontSide.Quality.IsAcceptable && input.FrontManualCorners is null)
            {
                canCompute = false;
                blocking = "Front 이미지 품질이 분석 기준에 미달합니다. 재촬영하거나 수동으로 카드 영역을 지정해 주세요.";
            }

            if (!frontSide.Detection.Success)
            {
                canCompute = false;
                blocking = frontSide.Detection.FailureReason ?? "카드 영역 검출에 실패했습니다.";
            }

            CenteringMeasurement? frontCentering = null;
            CenteringMeasurement? backCentering = null;
            var notes = new List<string>();
            var mode = CenteringMode.Auto;

            if (frontSide.NormalizedPath is not null)
            {
                using var normalizedFront = Cv2.ImRead(frontSide.NormalizedPath, ImreadModes.Color);
                var frontGuide = input.FrontCenteringGuide;
                if (frontGuide is not null)
                {
                    mode = CenteringMode.ManualAssist;
                }

                frontCentering = _centeringAnalyzer.Analyze(MatAdapter.Wrap(normalizedFront), CardSide.Front, frontGuide);
                if (frontCentering.Confidence < options.Centering.LowConfidenceThreshold && frontGuide is null)
                {
                    notes.Add("자동 센터링 분석 신뢰도가 낮습니다. 가이드를 확인해 주세요.");
                }

                using var overlay = CenteringOverlayRenderer.Render(normalizedFront, frontCentering, new Scalar(60, 200, 255));
                var overlayBytes = ImageCodec.EncodeJpeg(overlay);
                var overlayPath = await _imageStorage.SaveProcessedAsync(analysisId, "front_overlay.jpg", overlayBytes, cancellationToken);
                frontSide = CloneWithOverlay(frontSide, overlayPath);
                debug.Save("04_centering_lines_front", MatAdapter.Wrap(overlay));
            }

            if (backSide?.NormalizedPath is not null)
            {
                using var normalizedBack = Cv2.ImRead(backSide.NormalizedPath, ImreadModes.Color);
                var backGuide = input.BackCenteringGuide;
                if (backGuide is not null)
                {
                    mode = CenteringMode.ManualAssist;
                }

                backCentering = _centeringAnalyzer.Analyze(MatAdapter.Wrap(normalizedBack), CardSide.Back, backGuide);
                using var overlay = CenteringOverlayRenderer.Render(normalizedBack, backCentering, new Scalar(60, 200, 255));
                var overlayBytes = ImageCodec.EncodeJpeg(overlay);
                var overlayPath = await _imageStorage.SaveProcessedAsync(analysisId, "back_overlay.jpg", overlayBytes, cancellationToken);
                backSide = CloneWithOverlay(backSide, overlayPath);
            }

            var profile = _profileProvider.GetProfile(AppConstants.DefaultProfileName);
            var centeringCap = CenteringGradeCapCalculator.CalculateCap(frontCentering, backCentering, profile);
            _logger.LogInformation("Final centering grade cap = {Cap}", centeringCap);

            var centeringResult = new CenteringResult
            {
                Status = frontCentering is null ? AnalysisStatus.Failed : AnalysisStatus.Completed,
                Front = frontCentering,
                Back = backCentering,
                EstimatedGradeCap = centeringCap,
                Notes = notes,
                ModeUsed = mode
            };

            Report(progress, "코너 분석", "Corner 분석은 아직 구현되지 않았습니다.", 70);
            Report(progress, "엣지 분석", "Edge 분석은 아직 구현되지 않았습니다.", 75);
            Report(progress, "표면 분석", "Surface 분석은 아직 구현되지 않았습니다.", 80);

            var coverage = BuildCoverage(input, frontSide, backSide);
            var defects = new List<DetectedDefect>();
            if (frontCentering is not null && centeringCap < 10)
            {
                defects.Add(new DetectedDefect
                {
                    Type = DefectType.CenteringOff,
                    Severity = centeringCap <= 7 ? DefectSeverity.Moderate : DefectSeverity.Minor,
                    Side = CardSide.Front,
                    Confidence = frontCentering.Confidence,
                    Description = $"Front worst centering {frontCentering.WorstRatio:F1}% (L/R {frontCentering.LeftPercent:F1}/{frontCentering.RightPercent:F1}, T/B {frontCentering.TopPercent:F1}/{frontCentering.BottomPercent:F1})",
                    GradeCap = centeringCap,
                    Metrics = new Dictionary<string, double>
                    {
                        ["worstRatio"] = frontCentering.WorstRatio,
                        ["leftPercent"] = frontCentering.LeftPercent,
                        ["rightPercent"] = frontCentering.RightPercent,
                        ["topPercent"] = frontCentering.TopPercent,
                        ["bottomPercent"] = frontCentering.BottomPercent
                    }
                });
            }

            var critical = _criticalEvaluator.Evaluate(defects);
            var result = new CardAnalysisResult
            {
                AnalysisId = analysisId,
                Input = input,
                Front = frontSide,
                Back = backSide,
                Centering = centeringResult,
                Defects = defects,
                Coverage = coverage,
                CanComputeGrade = canCompute,
                BlockingReason = blocking,
                Warnings = warnings.Distinct().ToArray(),
                DebugImagePaths = debug.SnapshotPaths()
            };

            if (canCompute)
            {
                Report(progress, "예상 등급 계산", "PSA 규칙을 적용하는 중", 90);
                var grading = _gradingEngine.Calculate(result);
                if (critical.MaxGrade < grading.PredictedGrade)
                {
                    grading = ApplyCriticalCap(grading, critical);
                }

                result.Grading = grading;
                result.Explanation = _explanationService.Generate(result, grading);
            }

            Report(progress, "완료", canCompute ? "분석을 완료했습니다." : blocking ?? "분석을 완료하지 못했습니다.", 100);
            return result;
        }
        finally
        {
            backMat?.Dispose();
        }
    }

    private SideImageAnalysis AnalyzeSide(
        Guid analysisId,
        CardSide side,
        Mat source,
        string originalPath,
        QuadCorners? manualCorners,
        AnalysisOptions options,
        IDebugImageSink debug,
        IProgress<AnalysisProgress>? progress,
        double startPercent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var quality = _qualityAnalyzer.Analyze(MatAdapter.Wrap(source));
        Report(progress, "이미지 품질 확인", $"{side} 품질 점수 {quality.OverallQualityScore:F0}", startPercent);

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

        Report(progress, "카드 영역 검출", detection.Success ? $"{side} 카드 영역을 찾았습니다." : $"{side} 검출 실패", startPercent + 8);

        string? normalizedPath = null;
        var normalizedWidth = 0;
        var normalizedHeight = 0;
        if (detection is { Success: true, Corners: not null })
        {
            Report(progress, "카드 영역 검출", $"{side} Perspective Correction", startPercent + 12);
            var corrected = _perspectiveCorrector.Correct(MatAdapter.Wrap(source), detection.Corners, options.Normalization);
            var correctedMat = MatAdapter.Unwrap(corrected);
            try
            {
                debug.Save(side == CardSide.Front ? "03_perspective_front" : "03_perspective_back", corrected);
                var bytes = ImageCodec.EncodeJpeg(correctedMat);
                var fileName = side == CardSide.Front ? "front_normalized.jpg" : "back_normalized.jpg";
                normalizedPath = _imageStorage.SaveProcessedAsync(analysisId, fileName, bytes, cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                normalizedWidth = correctedMat.Width;
                normalizedHeight = correctedMat.Height;
            }
            finally
            {
                correctedMat.Dispose();
            }
        }

        return new SideImageAnalysis
        {
            Side = side,
            Quality = quality,
            Detection = detection,
            OriginalPath = originalPath,
            NormalizedPath = normalizedPath,
            NormalizedWidth = normalizedWidth,
            NormalizedHeight = normalizedHeight
        };
    }

    private static SideImageAnalysis CloneWithOverlay(SideImageAnalysis side, string overlayPath) =>
        new()
        {
            Side = side.Side,
            Quality = side.Quality,
            Detection = side.Detection,
            OriginalPath = side.OriginalPath,
            NormalizedPath = side.NormalizedPath,
            OverlayPath = overlayPath,
            NormalizedWidth = side.NormalizedWidth,
            NormalizedHeight = side.NormalizedHeight
        };

    private AnalysisOptions MergeOptions(ExpertAnalysisSettings settings)
    {
        _baseOptions.Card.WidthMm = settings.CardWidthMm;
        _baseOptions.Card.HeightMm = settings.CardHeightMm;
        _baseOptions.Quality.GlareValueThreshold = settings.GlareThreshold;
        _baseOptions.Quality.MinOverallQuality = settings.MinimumImageQuality;
        _baseOptions.Centering.LowConfidenceThreshold =
            Math.Clamp(0.55 - ((settings.CenteringAutoDetectSensitivity - 0.5) * 0.2), 0.3, 0.8);
        _baseOptions.Centering.PeakProminence =
            Math.Clamp(1.35 - ((settings.CenteringAutoDetectSensitivity - 0.5) * 0.3), 1.05, 1.8);
        return _baseOptions;
    }

    private static AnalysisCoverage BuildCoverage(CardInput input, SideImageAnalysis front, SideImageAnalysis? back)
    {
        var centering = (front.NormalizedPath is not null ? 0.6 : 0) + (back?.NormalizedPath is not null ? 0.4 : 0);
        return new AnalysisCoverage
        {
            Centering = centering * 100,
            Corners = 0,
            Edges = 0,
            Surface = 0,
            Overall = centering * 35,
            HasFront = true,
            HasBack = back is not null,
            HasAngledSurface = input.AdditionalImages.ContainsKey(ImageSlotKind.FrontSurfaceAngled)
                               || input.AdditionalImages.ContainsKey(ImageSlotKind.BackSurfaceAngled)
        };
    }

    private static GradingResult ApplyCriticalCap(GradingResult grading, GradeCapResult critical) =>
        new()
        {
            PredictedGrade = Math.Min(grading.PredictedGrade, critical.MaxGrade),
            GradeRangeMin = Math.Min(grading.GradeRangeMin, critical.MaxGrade),
            GradeRangeMax = Math.Min(grading.GradeRangeMax, critical.MaxGrade),
            AnalysisConfidence = grading.AnalysisConfidence,
            Centering = grading.Centering,
            Corners = grading.Corners,
            Edges = grading.Edges,
            Surface = grading.Surface,
            Defects = grading.Defects,
            GradeLimitReasons = grading.GradeLimitReasons.Concat(critical.Reasons).Distinct().ToArray(),
            Summary = grading.Summary,
            IsBorderline = grading.IsBorderline,
            PrimaryTenLimiter = grading.PrimaryTenLimiter ?? critical.Reasons.FirstOrDefault()
        };

    private static void Report(IProgress<AnalysisProgress>? progress, string stage, string message, double percent) =>
        progress?.Report(new AnalysisProgress { Stage = stage, Message = message, Percent = percent });
}
