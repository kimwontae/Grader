using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Explanations;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Imaging;
using OnePieceCardGrader.Imaging.Corners;
using OnePieceCardGrader.Imaging.Detection;
using OnePieceCardGrader.Imaging.Preprocessing;
using OnePieceCardGrader.Imaging.Quality;
using OnePieceCardGrader.Imaging.Surface;
using OnePieceCardGrader.Imaging.Visualization;
using OnePieceCardGrader.Imaging.Whitening;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace OnePieceCardGrader.Imaging.Pipeline;

public sealed class CardAnalysisPipeline : ICardAnalysisPipeline
{
    private readonly IImageQualityAnalyzer _qualityAnalyzer;
    private readonly ICardDetector _cardDetector;
    private readonly IPerspectiveCorrector _perspectiveCorrector;
    private readonly ICenteringAnalyzer _centeringAnalyzer;
    private readonly ICornerAnalyzer _cornerAnalyzer;
    private readonly IWhiteningAnalyzer _whiteningAnalyzer;
    private readonly ICornerGeometryAnalyzer _geometryAnalyzer;
    private readonly IScratchAnalyzer _scratchAnalyzer;
    private readonly ICriticalDefectEvaluator _criticalEvaluator;
    private readonly IGradingEngine _gradingEngine;
    private readonly IGradeExplanationService _explanationService;
    private readonly IImageStorage _imageStorage;
    private readonly IAppSettingsStore _settingsStore;
    private readonly AnalysisOptions _baseOptions;
    private readonly ImageAnalysisOptions _imageAnalysisOptions;
    private readonly IGradingProfileProvider _profileProvider;
    private readonly ILogger<CardAnalysisPipeline> _logger;

    public CardAnalysisPipeline(
        IImageQualityAnalyzer qualityAnalyzer,
        ICardDetector cardDetector,
        IPerspectiveCorrector perspectiveCorrector,
        ICenteringAnalyzer centeringAnalyzer,
        ICornerAnalyzer cornerAnalyzer,
        IWhiteningAnalyzer whiteningAnalyzer,
        ICornerGeometryAnalyzer geometryAnalyzer,
        IScratchAnalyzer scratchAnalyzer,
        ICriticalDefectEvaluator criticalEvaluator,
        IGradingEngine gradingEngine,
        IGradeExplanationService explanationService,
        IImageStorage imageStorage,
        IAppSettingsStore settingsStore,
        AnalysisOptions baseOptions,
        ImageAnalysisOptions imageAnalysisOptions,
        IGradingProfileProvider profileProvider,
        ILogger<CardAnalysisPipeline> logger)
    {
        _qualityAnalyzer = qualityAnalyzer;
        _cardDetector = cardDetector;
        _perspectiveCorrector = perspectiveCorrector;
        _centeringAnalyzer = centeringAnalyzer;
        _cornerAnalyzer = cornerAnalyzer;
        _whiteningAnalyzer = whiteningAnalyzer;
        _geometryAnalyzer = geometryAnalyzer;
        _scratchAnalyzer = scratchAnalyzer;
        _criticalEvaluator = criticalEvaluator;
        _gradingEngine = gradingEngine;
        _explanationService = explanationService;
        _imageStorage = imageStorage;
        _settingsStore = settingsStore;
        _baseOptions = baseOptions;
        _imageAnalysisOptions = imageAnalysisOptions;
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
        var imageOptions = ApplySensitivity(_imageAnalysisOptions, settings);
        var debugDir = Path.Combine(_imageStorage.GetAnalysisDirectory(analysisId), "debug");
        var debug = new FileDebugImageSink(debugDir, settings.DeveloperMode && settings.SaveDebugImages);
        var analysisDebug = new DirectoryDebugSink(debugDir, settings.DeveloperMode && settings.SaveDebugImages);

        Report(progress, AnalysisStageNames.Prepare, "분석 설정을 준비하는 중", 1);
        Report(progress, AnalysisStageNames.Front, "Front 이미지를 불러오는 중", 3);
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
            6,
            28,
            cancellationToken);

        SideImageAnalysis? backSide = null;
        Mat? backMat = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(input.BackImagePath) && File.Exists(input.BackImagePath))
            {
                Report(progress, AnalysisStageNames.Back, "Back 이미지를 불러오는 중", 29);
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
                    32,
                    46,
                    cancellationToken);
            }
            else
            {
                Report(progress, AnalysisStageNames.Back, "Back 사진이 없어 건너뜁니다.", 46);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, AnalysisStageNames.Centering, "Front/Back 센터링을 계산하는 중", 47);

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
                Report(progress, AnalysisStageNames.Centering, "Front 센터링을 계산하는 중", 49);
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

                debug.Save("04_centering_lines_front", MatAdapter.Wrap(normalizedFront));
            }

            if (backSide?.NormalizedPath is not null)
            {
                Report(progress, AnalysisStageNames.Centering, "Back 센터링을 계산하는 중", 53);
                using var normalizedBack = Cv2.ImRead(backSide.NormalizedPath, ImreadModes.Color);
                var backGuide = input.BackCenteringGuide;
                if (backGuide is not null)
                {
                    mode = CenteringMode.ManualAssist;
                }

                backCentering = _centeringAnalyzer.Analyze(MatAdapter.Wrap(normalizedBack), CardSide.Back, backGuide);
            }

            var profile = _profileProvider.GetProfile(AppConstants.DefaultProfileName);
            var centeringCap = CenteringGradeCapCalculator.CalculateCap(frontCentering, backCentering, profile);
            _logger.LogInformation("Final centering grade cap = {Cap}", centeringCap);

            Report(progress, AnalysisStageNames.Centering, "추가 사진을 저장하는 중", 56);
            var additional = await PersistAdditionalImagesAsync(analysisId, input, cancellationToken);

            var centeringResult = new CenteringResult
            {
                Status = frontCentering is null ? AnalysisStatus.Failed : AnalysisStatus.Completed,
                Front = frontCentering,
                Back = backCentering,
                EstimatedGradeCap = centeringCap,
                Notes = notes,
                ModeUsed = mode
            };

            Report(progress, AnalysisStageNames.Corners, "모서리 whitening/geometry를 검사하는 중", 58);
            var cornerAnalysis = AnalyzeCorners(frontSide, additional, input.AdditionalImageCorners, options, imageOptions, analysisDebug, progress, cancellationToken);

            Report(progress, AnalysisStageNames.Edges, "Edge whitening을 검사하는 중", 74);
            var edgeAnalysis = AnalyzeEdges(frontSide, imageOptions, analysisDebug, progress, cancellationToken);

            Report(progress, AnalysisStageNames.Surface, "스크래치 후보를 검사하는 중", 84);
            var surfaceAnalysis = AnalyzeSurface(frontSide, backSide, additional, analysisDebug, progress, cancellationToken);

            var coverage = BuildCoverage(input, frontSide, backSide, cornerAnalysis, edgeAnalysis, surfaceAnalysis);
            var defects = new List<DetectedDefect>();
            defects.AddRange(CreateCenteringDefects(frontCentering, backCentering, frontSide, backSide, options, profile));
            defects.AddRange(cornerAnalysis.Corners.SelectMany(c => c.Defects));
            defects.AddRange(edgeAnalysis.Defects);
            defects.AddRange(surfaceAnalysis.Defects);

            if (frontSide.NormalizedPath is not null)
            {
                Report(progress, AnalysisStageNames.Grading, "Front 오버레이를 생성하는 중", 94);
                using var normalizedFront = Cv2.ImRead(frontSide.NormalizedPath, ImreadModes.Color);
                using var overlay = frontCentering is null
                    ? normalizedFront.Clone()
                    : CenteringOverlayRenderer.Render(normalizedFront, frontCentering, new Scalar(60, 200, 255));
                DefectOverlayRenderer.Draw(overlay, defects.Where(d => d.Side == CardSide.Front));
                var overlayBytes = ImageCodec.EncodeJpeg(overlay);
                var overlayPath = await _imageStorage.SaveProcessedAsync(analysisId, "front_overlay.jpg", overlayBytes, cancellationToken);
                frontSide = CloneWithOverlay(frontSide, overlayPath);
            }

            if (backSide?.NormalizedPath is not null)
            {
                Report(progress, AnalysisStageNames.Grading, "Back 오버레이를 생성하는 중", 96);
                using var normalizedBack = Cv2.ImRead(backSide.NormalizedPath, ImreadModes.Color);
                using var overlay = backCentering is null
                    ? normalizedBack.Clone()
                    : CenteringOverlayRenderer.Render(normalizedBack, backCentering, new Scalar(60, 200, 255));
                DefectOverlayRenderer.Draw(overlay, defects.Where(d => d.Side == CardSide.Back));
                var overlayBytes = ImageCodec.EncodeJpeg(overlay);
                var overlayPath = await _imageStorage.SaveProcessedAsync(analysisId, "back_overlay.jpg", overlayBytes, cancellationToken);
                backSide = CloneWithOverlay(backSide, overlayPath);
            }

            await AttachZoomImagesAsync(analysisId, defects, frontSide, backSide, cancellationToken);

            var critical = _criticalEvaluator.Evaluate(defects);
            var result = new CardAnalysisResult
            {
                AnalysisId = analysisId,
                Input = input,
                Front = frontSide,
                Back = backSide,
                Centering = centeringResult,
                Corners = cornerAnalysis,
                Edges = edgeAnalysis,
                Surface = surfaceAnalysis,
                Defects = defects,
                Coverage = coverage,
                CanComputeGrade = canCompute,
                BlockingReason = blocking,
                Warnings = warnings.Distinct().ToArray(),
                DebugImagePaths = MergeDebug(debug.SnapshotPaths(), analysisDebug.SnapshotPaths())
            };

            if (canCompute)
            {
                Report(progress, AnalysisStageNames.Grading, "PSA 규칙을 적용하는 중", 98);
                var grading = _gradingEngine.Calculate(result);
                if (critical.MaxGrade < grading.PredictedGrade)
                {
                    grading = ApplyCriticalCap(grading, critical);
                }

                result.Grading = grading;
                result.Explanation = _explanationService.Generate(result, grading);
            }
            else
            {
                Report(progress, AnalysisStageNames.Grading, blocking ?? "등급을 계산할 수 없어 건너뜁니다.", 99);
            }

            Report(progress, AnalysisStageNames.Done, canCompute ? "분석을 완료했습니다." : blocking ?? "분석을 완료하지 못했습니다.", 100);
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
        double fromPercent,
        double toPercent,
        CancellationToken cancellationToken)
    {
        var stage = side == CardSide.Front ? AnalysisStageNames.Front : AnalysisStageNames.Back;
        cancellationToken.ThrowIfCancellationRequested();
        Report(progress, stage, $"{side} 이미지 품질을 검사하는 중", Map(fromPercent, toPercent, 0, 4));
        var quality = _qualityAnalyzer.Analyze(MatAdapter.Wrap(source));
        Report(progress, stage, $"{side} 품질 점수 {quality.OverallQualityScore:F0}", Map(fromPercent, toPercent, 1, 4));

        Report(progress, stage, $"{side} 카드 영역을 찾는 중", Map(fromPercent, toPercent, 1.3, 4));
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

        Report(progress, stage, detection.Success ? $"{side} 카드 영역을 찾았습니다." : $"{side} 검출 실패", Map(fromPercent, toPercent, 2, 4));

        string? normalizedPath = null;
        var normalizedWidth = 0;
        var normalizedHeight = 0;
        if (detection is { Success: true, Corners: not null })
        {
            Report(progress, stage, $"{side} 원근 보정 중", Map(fromPercent, toPercent, 2.5, 4));
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

        Report(progress, stage, $"{side} 이미지 처리를 완료했습니다.", toPercent);
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
        _baseOptions.Corners.Sensitivity = settings.CornerDetectionSensitivity;
        _baseOptions.Surface.ScratchSensitivity = settings.SurfaceScratchSensitivity;
        return _baseOptions;
    }

    private async Task<Dictionary<ImageSlotKind, string>> PersistAdditionalImagesAsync(
        Guid analysisId,
        CardInput input,
        CancellationToken cancellationToken)
    {
        var saved = new Dictionary<ImageSlotKind, string>();
        foreach (var pair in input.AdditionalImages)
        {
            if (string.IsNullOrWhiteSpace(pair.Value) || !File.Exists(pair.Value))
            {
                continue;
            }

            saved[pair.Key] = await _imageStorage.SaveOriginalAsync(
                analysisId,
                pair.Key.ToString().ToLowerInvariant(),
                pair.Value,
                cancellationToken);
        }

        return saved;
    }

    private CornerAnalysisResult AnalyzeCorners(
        SideImageAnalysis front,
        IReadOnlyDictionary<ImageSlotKind, string> additional,
        IReadOnlyDictionary<ImageSlotKind, QuadCorners> macroCorners,
        AnalysisOptions options,
        ImageAnalysisOptions imageOptions,
        IAnalysisDebugSink analysisDebug,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (front.NormalizedPath is null || !File.Exists(front.NormalizedPath))
        {
            Report(progress, AnalysisStageNames.Corners, "정규화된 이미지가 없어 코너 분석을 건너뜁니다.", 74);
            return new CornerAnalysisResult
            {
                Status = AnalysisStatus.Skipped,
                UnavailableReason = "정규화된 Front 이미지가 없어 코너 분석을 건너뛰었습니다."
            };
        }

        Report(progress, AnalysisStageNames.Corners, "모서리 검사 영역을 준비하는 중", 59);
        using var normalized = Cv2.ImRead(front.NormalizedPath, ImreadModes.Color);
        var glareDet = GlareDetector.Analyze(normalized, options.Quality);
        using var glare = new GlareMask(
            glareDet.Mask ?? new Mat(normalized.Size(), MatType.CV_8UC1, Scalar.All(0)),
            glareDet.CoverageRatio);
        using var cardMask = CardMaskExtractor.Extract(normalized, imageOptions.CornerGeometry.CardMaskThreshold);
        var context = new MeasurementContext
        {
            ImageQuality = front.Quality.OverallQualityScore,
            Focus = front.Quality.FocusScore,
            Exposure = front.Quality.ExposureScore,
            CardDetectionConfidence = front.Detection.Confidence
        };

        var results = new List<CornerResult>();
        var positions = Enum.GetValues<CornerPosition>();
        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];
            cancellationToken.ThrowIfCancellationRequested();
            Report(
                progress,
                AnalysisStageNames.Corners,
                $"{CornerLabel(position)} 모서리 검사 중 ({i + 1}/{positions.Length})",
                Map(58, 74, i, positions.Length));
            var slot = position switch
            {
                CornerPosition.TopLeft => ImageSlotKind.FrontCornerTopLeft,
                CornerPosition.TopRight => ImageSlotKind.FrontCornerTopRight,
                CornerPosition.BottomRight => ImageSlotKind.FrontCornerBottomRight,
                _ => ImageSlotKind.FrontCornerBottomLeft
            };

            var roi = CornerRoi.FromNormalized(normalized.Width, normalized.Height, position, options.Corners);
            var region = CornerRoi.ToNormalized(roi, normalized.Width, normalized.Height);
            var hasMacro = additional.TryGetValue(slot, out var macroPath) && !string.IsNullOrWhiteSpace(macroPath);
            if (hasMacro)
            {
                macroCorners.TryGetValue(slot, out var cardQuad);
                using var macro = ExifOrientation.LoadOriented(macroPath!);
                results.Add(_cornerAnalyzer.Analyze(MatAdapter.Wrap(macro), position, CardSide.Front, true, region, cardQuad));
            }
            else
            {
                var whiteningDebug = new PrefixedAnalysisDebugSink(analysisDebug, $"whitening/{position}");
                var geometryDebug = new PrefixedAnalysisDebugSink(analysisDebug, $"corners/{position}");
                var whitening = _whiteningAnalyzer.Analyze(
                    normalized,
                    WhiteningRegionMap.FromCorner(position),
                    glare,
                    context,
                    whiteningDebug);
                var geometry = _geometryAnalyzer.Analyze(normalized, position, cardMask, context, geometryDebug);
                var combinedSeverity = CornerGeometryScoreCalculator.CombineWhiteningAndGeometry(whitening.Severity, geometry.Severity);
                var combinedScore = CornerGeometryScoreCalculator.ComputeCombinedConditionScore(
                    combinedSeverity,
                    imageOptions.CornerGeometry);
                var defects = new List<DetectedDefect>();
                var wDefect = DefectFactory.FromWhitening(whitening, CardSide.Front, position.ToString(), DefectType.CornerWhitening, region);
                if (wDefect is not null)
                {
                    defects.Add(wDefect);
                }

                var gDefect = DefectFactory.FromGeometry(geometry, CardSide.Front, region);
                if (gDefect is not null)
                {
                    defects.Add(gDefect);
                }

                results.Add(new CornerResult
                {
                    Position = position,
                    Side = CardSide.Front,
                    SharpnessScore = front.Quality.FocusScore,
                    WhiteningScore = whitening.ConditionScore,
                    GeometryScore = geometry.ConditionScore,
                    CombinedScore = combinedScore,
                    Severity = DefectFactory.FromSeverity(combinedSeverity),
                    Confidence = Math.Clamp(((whitening.Confidence + geometry.Confidence) / 2.0) / 100.0, 0, 1),
                    Defects = defects,
                    Status = AnalysisStatus.Completed,
                    Region = region,
                    UsedMacroImage = false,
                    WhiteningSeverity = whitening.Severity,
                    GeometrySeverity = geometry.Severity,
                    Whitening = whitening,
                    Geometry = geometry
                });
            }
        }

        Report(progress, AnalysisStageNames.Corners, "모서리 검사를 완료했습니다.", 74);
        var scores = results.Select(r => r.CombinedScore).ToArray();
        return new CornerAnalysisResult
        {
            Status = AnalysisStatus.Completed,
            Corners = results,
            ConditionScore = CornerGeometryScoreCalculator.ComputeFinalCornerScore(scores, imageOptions.CornerGeometry),
            Confidence = results.Count == 0 ? 0 : results.Average(r => r.Confidence),
            GradeCap = DefectScoreMath.GradeCap(results.SelectMany(r => r.Defects))
        };
    }

    private EdgeAnalysisResult AnalyzeEdges(
        SideImageAnalysis front,
        ImageAnalysisOptions imageOptions,
        IAnalysisDebugSink analysisDebug,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (front.NormalizedPath is null || !File.Exists(front.NormalizedPath))
        {
            Report(progress, AnalysisStageNames.Edges, "정규화된 이미지가 없어 엣지 분석을 건너뜁니다.", 84);
            return new EdgeAnalysisResult
            {
                Status = AnalysisStatus.Skipped,
                UnavailableReason = "정규화된 Front 이미지가 없어 엣지 분석을 건너뛰었습니다."
            };
        }

        Report(progress, AnalysisStageNames.Edges, "엣지 검사 영역을 준비하는 중", 75);
        using var normalized = Cv2.ImRead(front.NormalizedPath, ImreadModes.Color);
        var glareDet = GlareDetector.Analyze(normalized, _baseOptions.Quality);
        using var glare = new GlareMask(
            glareDet.Mask ?? new Mat(normalized.Size(), MatType.CV_8UC1, Scalar.All(0)),
            glareDet.CoverageRatio);
        var context = new MeasurementContext
        {
            ImageQuality = front.Quality.OverallQualityScore,
            Focus = front.Quality.FocusScore,
            Exposure = front.Quality.ExposureScore,
            CardDetectionConfidence = front.Detection.Confidence
        };

        var edges = new List<EdgeResult>();
        var defects = new List<DetectedDefect>();
        var positions = Enum.GetValues<EdgePosition>();
        for (var i = 0; i < positions.Length; i++)
        {
            var position = positions[i];
            cancellationToken.ThrowIfCancellationRequested();
            Report(
                progress,
                AnalysisStageNames.Edges,
                $"{EdgeLabel(position)} 엣지 검사 중 ({i + 1}/{positions.Length})",
                Map(74, 84, i, positions.Length));
            var whitening = _whiteningAnalyzer.Analyze(
                normalized,
                WhiteningRegionMap.FromEdge(position),
                glare,
                context,
                new PrefixedAnalysisDebugSink(analysisDebug, $"whitening/{position}"));
            var defect = DefectFactory.FromWhitening(
                whitening,
                CardSide.Front,
                position.ToString(),
                DefectType.EdgeWhitening,
                null);
            var localDefects = defect is null ? Array.Empty<DetectedDefect>() : new[] { defect };
            defects.AddRange(localDefects);
            edges.Add(new EdgeResult
            {
                Position = position,
                Side = CardSide.Front,
                Score = whitening.ConditionScore,
                WhiteningCount = whitening.DefectCount,
                AffectedLengthPx = whitening.WhiteningLengthRatio,
                Severity = DefectFactory.FromSeverity(whitening.Severity),
                Confidence = whitening.Confidence / 100.0,
                Defects = localDefects,
                Status = AnalysisStatus.Completed,
                Whitening = whitening
            });
        }

        Report(progress, AnalysisStageNames.Edges, "엣지 검사를 완료했습니다.", 84);
        var condition = CornerGeometryScoreCalculator.ComputeFinalCornerScore(
            edges.Select(e => e.Score).ToArray(),
            imageOptions.CornerGeometry);
        return new EdgeAnalysisResult
        {
            Status = AnalysisStatus.Completed,
            Edges = edges,
            ConditionScore = condition,
            Confidence = edges.Count == 0 ? 0 : edges.Average(e => e.Confidence),
            GradeCap = DefectScoreMath.GradeCap(defects),
            Defects = defects
        };
    }

    private SurfaceAnalysisResult AnalyzeSurface(
        SideImageAnalysis front,
        SideImageAnalysis? back,
        IReadOnlyDictionary<ImageSlotKind, string> additional,
        IAnalysisDebugSink analysisDebug,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hasDedicated = additional.ContainsKey(ImageSlotKind.FrontSurfaceNormal)
                           || additional.ContainsKey(ImageSlotKind.FrontSurfaceAngled)
                           || additional.ContainsKey(ImageSlotKind.BackSurfaceNormal)
                           || additional.ContainsKey(ImageSlotKind.BackSurfaceAngled);
        var set = new SurfaceImageSet
        {
            NormalFrontPath = additional.GetValueOrDefault(ImageSlotKind.FrontSurfaceNormal) ?? front.NormalizedPath,
            AngledFrontPath = additional.GetValueOrDefault(ImageSlotKind.FrontSurfaceAngled),
            NormalBackPath = additional.GetValueOrDefault(ImageSlotKind.BackSurfaceNormal) ?? back?.NormalizedPath,
            AngledBackPath = additional.GetValueOrDefault(ImageSlotKind.BackSurfaceAngled),
            UsedNormalizedFallback = !hasDedicated
        };

        var context = new MeasurementContext
        {
            ImageQuality = front.Quality.OverallQualityScore,
            Focus = front.Quality.FocusScore,
            Exposure = front.Quality.ExposureScore,
            CardDetectionConfidence = front.Detection.Confidence,
            HasAngledLight = set.AngledFrontPath is not null || set.AngledBackPath is not null
        };

        ScratchAnalysisResult? frontScratch = null;
        if (!string.IsNullOrWhiteSpace(set.NormalFrontPath))
        {
            Report(progress, AnalysisStageNames.Surface, "Front 스크래치 후보를 검사하는 중", 85);
            frontScratch = _scratchAnalyzer.Analyze(set, CardSide.Front, context, new PrefixedAnalysisDebugSink(analysisDebug, "scratch/front"));
        }

        cancellationToken.ThrowIfCancellationRequested();
        ScratchAnalysisResult? backScratch = null;
        if (!string.IsNullOrWhiteSpace(set.NormalBackPath))
        {
            Report(progress, AnalysisStageNames.Surface, "Back 스크래치 후보를 검사하는 중", 89);
            backScratch = _scratchAnalyzer.Analyze(set, CardSide.Back, context, new PrefixedAnalysisDebugSink(analysisDebug, "scratch/back"));
        }

        if (frontScratch is null && backScratch is null)
        {
            Report(progress, AnalysisStageNames.Surface, "표면 분석용 이미지가 없어 건너뜁니다.", 94);
            return new SurfaceAnalysisResult
            {
                Status = AnalysisStatus.Skipped,
                UnavailableReason = "Surface 분석용 이미지가 없습니다.",
                Confidence = 0
            };
        }

        Report(progress, AnalysisStageNames.Surface, "표면 검사를 완료했습니다.", 94);

        var defects = new List<DetectedDefect>();
        if (frontScratch is not null)
        {
            defects.AddRange(frontScratch.Candidates.Select(c => DefectFactory.FromScratch(c, CardSide.Front)));
        }

        if (backScratch is not null)
        {
            defects.AddRange(backScratch.Candidates.Select(c => DefectFactory.FromScratch(c, CardSide.Back)));
        }

        var frontScore = frontScratch?.ConditionScore ?? 100;
        var backScore = backScratch?.ConditionScore ?? 100;
        var usedAngled = (frontScratch?.Alignment?.UsedAngledComparison ?? false)
                         || (backScratch?.Alignment?.UsedAngledComparison ?? false);
        var confidence = Math.Max(frontScratch?.Confidence ?? 0, backScratch?.Confidence ?? 0) / 100.0
                         * (set.UsedNormalizedFallback ? 0.8 : 1.0);

        return new SurfaceAnalysisResult
        {
            Status = AnalysisStatus.Completed,
            FrontScore = frontScore,
            BackScore = backScore,
            ConditionScore = Math.Min(frontScore, backScore),
            GradeCap = DefectScoreMath.GradeCap(defects),
            Confidence = Math.Clamp(confidence, 0.15, 0.95),
            Defects = defects,
            UsedAngledLight = usedAngled,
            UsedMacroFallback = set.UsedNormalizedFallback,
            FrontScratch = frontScratch,
            BackScratch = backScratch
        };
    }

    private static AnalysisCoverage BuildCoverage(
        CardInput input,
        SideImageAnalysis front,
        SideImageAnalysis? back,
        CornerAnalysisResult corners,
        EdgeAnalysisResult edges,
        SurfaceAnalysisResult surface)
    {
        var centering = (front.NormalizedPath is not null ? 0.6 : 0) + (back?.NormalizedPath is not null ? 0.4 : 0);
        var macroCorners = new[]
        {
            ImageSlotKind.FrontCornerTopLeft,
            ImageSlotKind.FrontCornerTopRight,
            ImageSlotKind.FrontCornerBottomLeft,
            ImageSlotKind.FrontCornerBottomRight
        }.Count(slot => input.AdditionalImages.ContainsKey(slot));
        var cornerCoverage = corners.Status == AnalysisStatus.Completed
            ? 55 + (macroCorners * 11.25)
            : 0;
        var edgeCoverage = edges.Status == AnalysisStatus.Completed ? 80 : 0;
        var surfaceCoverage = surface.Status == AnalysisStatus.Completed
            ? (surface.UsedAngledLight ? 90 : surface.UsedMacroFallback ? 55 : 80)
            : 0;
        var overall = (centering * 30)
                      + (Math.Min(cornerCoverage, 100) / 100.0 * 25)
                      + (edgeCoverage / 100.0 * 15)
                      + (surfaceCoverage / 100.0 * 30);
        return new AnalysisCoverage
        {
            Centering = centering * 100,
            Corners = Math.Min(cornerCoverage, 100),
            Edges = edgeCoverage,
            Surface = surfaceCoverage,
            Overall = Math.Clamp(overall, 0, 100),
            HasFront = true,
            HasBack = back is not null,
            HasAngledSurface = input.AdditionalImages.ContainsKey(ImageSlotKind.FrontSurfaceAngled)
                               || input.AdditionalImages.ContainsKey(ImageSlotKind.BackSurfaceAngled),
            MacroCornerCount = macroCorners
        };
    }

    private static ImageAnalysisOptions ApplySensitivity(ImageAnalysisOptions source, ExpertAnalysisSettings settings)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        var clone = System.Text.Json.JsonSerializer.Deserialize<ImageAnalysisOptions>(json) ?? new ImageAnalysisOptions();
        var edgeScale = 1.20 - (settings.EdgeWhiteningSensitivity * 0.4);
        var cornerScale = 1.15 - (settings.CornerDetectionSensitivity * 0.3);
        var scratchScale = 1.20 - (settings.SurfaceScratchSensitivity * 0.4);
        clone.Whitening.MinDeltaE *= edgeScale;
        clone.Whitening.MinBrightnessIncrease *= edgeScale;
        clone.Whitening.MinWhiteningPixelScore *= Math.Clamp(edgeScale, 0.7, 1.3);
        clone.CornerGeometry.MissingAreaReference *= cornerScale;
        clone.CornerGeometry.MaxNormalDeviationRatio *= cornerScale;
        clone.Scratch.CandidateScoreThreshold *= scratchScale;
        clone.Scratch.MinLocalContrast *= scratchScale;
        return clone;
    }

    private static IReadOnlyDictionary<string, string> MergeDebug(
        IReadOnlyDictionary<string, string> first,
        IReadOnlyDictionary<string, string> second)
    {
        var merged = new Dictionary<string, string>(first, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in second)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static IEnumerable<DetectedDefect> CreateCenteringDefects(
        CenteringMeasurement? front,
        CenteringMeasurement? back,
        SideImageAnalysis frontSide,
        SideImageAnalysis? backSide,
        AnalysisOptions options,
        GradingProfile profile)
    {
        var defects = new List<DetectedDefect>();
        profile.Centering.TryGetValue("10", out var psa10);
        var frontMax = psa10?.FrontMax ?? 55;
        var backMax = psa10?.BackMax ?? 75;

        if (front is not null)
        {
            var cap = CenteringGradeCapCalculator.CalculateCap(front, null, profile);
            if (cap < 10)
            {
                defects.Add(CreateCenteringDefect(
                    CardSide.Front,
                    front,
                    cap,
                    frontSide.NormalizedWidth,
                    frontSide.NormalizedHeight,
                    options.Card.WidthMm,
                    options.Card.HeightMm,
                    frontMax));
            }
        }

        if (back is not null && backSide is not null)
        {
            var cap = CenteringGradeCapCalculator.CalculateCap(null, back, profile);
            if (cap < 10)
            {
                defects.Add(CreateCenteringDefect(
                    CardSide.Back,
                    back,
                    cap,
                    backSide.NormalizedWidth,
                    backSide.NormalizedHeight,
                    options.Card.WidthMm,
                    options.Card.HeightMm,
                    backMax));
            }
        }

        return defects;
    }

    private static DetectedDefect CreateCenteringDefect(
        CardSide side,
        CenteringMeasurement measurement,
        int gradeCap,
        double imageWidth,
        double imageHeight,
        double cardWidthMm,
        double cardHeightMm,
        double psa10Max)
    {
        var width = imageWidth > 0 ? imageWidth : 1260;
        var height = imageHeight > 0 ? imageHeight : 1760;
        var narration = DefectNarrator.ForCentering(
            side,
            measurement,
            gradeCap,
            cardWidthMm,
            cardHeightMm,
            width,
            height,
            psa10Max);
        return new DetectedDefect
        {
            Type = DefectType.CenteringOff,
            Severity = gradeCap <= 7 ? DefectSeverity.Moderate : DefectSeverity.Minor,
            Side = side,
            Confidence = measurement.Confidence,
            Title = narration.Title,
            Description = narration.Description,
            Impact = narration.Impact,
            GradeCap = gradeCap,
            Metrics = new Dictionary<string, double>
            {
                ["worstRatio"] = measurement.WorstRatio,
                ["leftPercent"] = measurement.LeftPercent,
                ["rightPercent"] = measurement.RightPercent,
                ["topPercent"] = measurement.TopPercent,
                ["bottomPercent"] = measurement.BottomPercent,
                ["leftMm"] = measurement.LeftMarginPx / width * cardWidthMm,
                ["rightMm"] = measurement.RightMarginPx / width * cardWidthMm,
                ["topMm"] = measurement.TopMarginPx / height * cardHeightMm,
                ["bottomMm"] = measurement.BottomMarginPx / height * cardHeightMm
            }
        };
    }

    private async Task AttachZoomImagesAsync(
        Guid analysisId,
        IEnumerable<DetectedDefect> defects,
        SideImageAnalysis? front,
        SideImageAnalysis? back,
        CancellationToken cancellationToken)
    {
        Mat? frontMat = null;
        Mat? backMat = null;
        try
        {
            if (front?.NormalizedPath is not null && File.Exists(front.NormalizedPath))
            {
                frontMat = Cv2.ImRead(front.NormalizedPath, ImreadModes.Color);
            }

            if (back?.NormalizedPath is not null && File.Exists(back.NormalizedPath))
            {
                backMat = Cv2.ImRead(back.NormalizedPath, ImreadModes.Color);
            }

            var index = 0;
            foreach (var defect in defects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (defect.Region is null || defect.Type == DefectType.CenteringOff)
                {
                    continue;
                }

                var source = defect.Side == CardSide.Back ? backMat : frontMat;
                if (source is null || source.Empty())
                {
                    continue;
                }

                using var zoom = DefectZoomRenderer.Crop(source, defect.Region, "X");
                if (zoom is null)
                {
                    continue;
                }

                var bytes = ImageCodec.EncodeJpeg(zoom);
                defect.ZoomImagePath = await _imageStorage.SaveProcessedAsync(
                    analysisId,
                    $"defect_{index:00}_{defect.Type}.jpg",
                    bytes,
                    cancellationToken);
                index++;
            }
        }
        finally
        {
            frontMat?.Dispose();
            backMat?.Dispose();
        }
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
        progress?.Report(new AnalysisProgress
        {
            Stage = stage,
            Message = message,
            Percent = Math.Clamp(percent, 0, 100)
        });

    private static double Map(double from, double to, double index, int count)
    {
        if (count <= 0)
        {
            return to;
        }

        return from + ((to - from) * (index / count));
    }

    private static string CornerLabel(CornerPosition position) => position switch
    {
        CornerPosition.TopLeft => "좌상단",
        CornerPosition.TopRight => "우상단",
        CornerPosition.BottomRight => "우하단",
        _ => "좌하단"
    };

    private static string EdgeLabel(EdgePosition position) => position switch
    {
        EdgePosition.Top => "상단",
        EdgePosition.Right => "우측",
        EdgePosition.Bottom => "하단",
        _ => "좌측"
    };
}
