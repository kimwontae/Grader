using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnePieceCardGrader.App.Services;
using OnePieceCardGrader.Core.Calculations;
using OnePieceCardGrader.Core.Constants;
using OnePieceCardGrader.Core.DTOs;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.App.ViewModels;

public partial class WizardViewModel : ObservableObject
{
    private readonly ICardPreviewService _previewService;
    private readonly ICardAnalysisPipeline _pipeline;
    private readonly IAnalysisRepository _repository;
    private CancellationTokenSource? _cts;

    public WizardViewModel(
        ICardPreviewService previewService,
        ICardAnalysisPipeline pipeline,
        IAnalysisRepository repository)
    {
        _previewService = previewService;
        _pipeline = pipeline;
        _repository = repository;
        Disclaimer = AppConstants.DisclaimerKo;
        ConfidenceMeaning = AppConstants.ConfidenceMeaningKo;
        ResetAnalysisStages();
    }

    public string Disclaimer { get; }
    public string ConfidenceMeaning { get; }
    public ObservableCollection<string> ProgressLog { get; } = [];
    public ObservableCollection<AnalysisStageItem> AnalysisStages { get; } = [];

    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    [ObservableProperty] private int _stepIndex;
    [ObservableProperty] private string _cardName = string.Empty;
    [ObservableProperty] private string _cardNumber = string.Empty;
    [ObservableProperty] private string _setName = string.Empty;
    [ObservableProperty] private string _language = "JP";
    [ObservableProperty] private string _rarity = string.Empty;
    [ObservableProperty] private string _memo = string.Empty;
    [ObservableProperty] private string? _frontImagePath;
    [ObservableProperty] private string? _backImagePath;
    [ObservableProperty] private CardPreviewResult? _frontPreview;
    [ObservableProperty] private CardPreviewResult? _backPreview;
    [ObservableProperty] private QuadCorners? _frontCorners;
    [ObservableProperty] private QuadCorners? _backCorners;
    [ObservableProperty] private CenteringGuide? _frontGuide;
    [ObservableProperty] private CenteringGuide? _backGuide;
    [ObservableProperty] private string _activeSide = "Front";
    [ObservableProperty] private double _zoom = 1;
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(BackCommand))]
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "카드 정보를 입력한 뒤 Front/Back 사진을 업로드하세요.";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressStage = string.Empty;
    [ObservableProperty] private string _progressMessage = string.Empty;
    [ObservableProperty] private string _progressPercentText = "0%";
    [ObservableProperty] private string _progressBarLabel = "분석을 시작합니다...";
    [ObservableProperty] private CardAnalysisResult? _result;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _frontRatioText = "-";
    [ObservableProperty] private string _backRatioText = "-";
    [ObservableProperty] private bool _lowCenteringConfidence;

    public string StepTitle => StepIndex switch
    {
        0 => "1. 카드 정보",
        1 => "2. 전체 사진",
        2 => "3. 카드 영역 검출",
        3 => "4. 센터링 가이드",
        4 => "5. 모서리 확대 (선택)",
        5 => "6. 표면/상처 사진 (선택)",
        6 => "7. 분석",
        _ => "8. 결과"
    };

    public bool CanGoNext => !IsBusy && StepIndex is >= 0 and < 6;
    public bool CanGoBack => !IsBusy && StepIndex > 0 && StepIndex != 6;
    public bool IsInfoStep => StepIndex == 0;
    public bool IsPhotoStep => StepIndex == 1;
    public bool IsDetectionStep => StepIndex == 2;
    public bool IsCenteringStep => StepIndex == 3;
    public bool IsCornerStep => StepIndex == 4;
    public bool IsSurfaceStep => StepIndex == 5;
    public bool IsProgressStep => StepIndex == 6;
    public bool IsResultStep => StepIndex == 7;
    public string NextButtonText => StepIndex == 5 ? "분석 시작" : "다음 / 적용";

    [ObservableProperty] private string? _frontCornerTopLeftPath;
    [ObservableProperty] private string? _frontCornerTopRightPath;
    [ObservableProperty] private string? _frontCornerBottomLeftPath;
    [ObservableProperty] private string? _frontCornerBottomRightPath;
    [ObservableProperty] private string? _frontSurfaceNormalPath;
    [ObservableProperty] private string? _frontSurfaceAngledPath;
    [ObservableProperty] private string? _backSurfaceNormalPath;
    [ObservableProperty] private string? _backSurfaceAngledPath;

    partial void OnStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsInfoStep));
        OnPropertyChanged(nameof(IsPhotoStep));
        OnPropertyChanged(nameof(IsDetectionStep));
        OnPropertyChanged(nameof(IsCenteringStep));
        OnPropertyChanged(nameof(IsCornerStep));
        OnPropertyChanged(nameof(IsSurfaceStep));
        OnPropertyChanged(nameof(IsProgressStep));
        OnPropertyChanged(nameof(IsResultStep));
        OnPropertyChanged(nameof(NextButtonText));
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
    }

    partial void OnFrontGuideChanged(CenteringGuide? value) => RecalculateRatios();
    partial void OnBackGuideChanged(CenteringGuide? value) => RecalculateRatios();
    partial void OnFrontPreviewChanged(CardPreviewResult? value) => RecalculateRatios();
    partial void OnBackPreviewChanged(CardPreviewResult? value) => RecalculateRatios();

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private async Task NextAsync()
    {
        ErrorMessage = null;
        if (StepIndex == 0 && string.IsNullOrWhiteSpace(CardName))
        {
            ErrorMessage = "Card Name은 필수입니다.";
            return;
        }

        if (StepIndex == 1)
        {
            if (string.IsNullOrWhiteSpace(FrontImagePath))
            {
                ErrorMessage = "Front 전체 사진은 필수입니다.";
                return;
            }

            await PrepareDetectionAsync();
        }

        if (StepIndex == 2)
        {
            await ApplyDetectionAsync();
        }

        if (StepIndex == 5)
        {
            await RunAnalysisAsync();
            return;
        }

        if (StepIndex < 5)
        {
            StepIndex++;
        }
    }

    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private void Back()
    {
        if (CanGoBack)
        {
            StepIndex--;
        }
    }

    [RelayCommand]
    private async Task AutoDetectAsync()
    {
        await PrepareDetectionAsync(forceAuto: true);
    }

    [RelayCommand]
    private void ResetCorners()
    {
        if (ActiveSide == "Front" && FrontPreview is not null)
        {
            FrontCorners = DefaultInset(FrontPreview.Detection.ImageWidth, FrontPreview.Detection.ImageHeight);
        }

        if (ActiveSide == "Back" && BackPreview is not null)
        {
            BackCorners = DefaultInset(BackPreview.Detection.ImageWidth, BackPreview.Detection.ImageHeight);
        }
    }

    [RelayCommand]
    private async Task ApplyDetectionAsync()
    {
        if (!string.IsNullOrWhiteSpace(FrontImagePath))
        {
            FrontPreview = await _previewService.CreateAsync(FrontImagePath, CardSide.Front, FrontCorners, FrontGuide, CancellationToken.None);
            FrontGuide = FrontPreview.Centering?.Guide?.Clone() ?? CenteringGuide.CreateDefault();
        }

        if (!string.IsNullOrWhiteSpace(BackImagePath))
        {
            BackPreview = await _previewService.CreateAsync(BackImagePath, CardSide.Back, BackCorners, BackGuide, CancellationToken.None);
            BackGuide = BackPreview.Centering?.Guide?.Clone() ?? CenteringGuide.CreateDefault();
        }

        StatusMessage = "카드 영역을 적용했습니다. 센터링 가이드를 확인하세요.";
        RecalculateRatios();
    }

    [RelayCommand]
    private void SetZoom(string value)
    {
        if (double.TryParse(value, out var zoom))
        {
            Zoom = zoom;
        }
    }

    [RelayCommand]
    private void CancelAnalysis() => _cts?.Cancel();

    [RelayCommand]
    private async Task ReanalyzeAsync()
    {
        await RunAnalysisAsync();
    }

    [RelayCommand]
    private async Task SaveActualGradeAsync(string gradeText)
    {
        if (Result is null || !int.TryParse(gradeText, out var grade))
        {
            return;
        }

        await _repository.SaveActualGradeAsync(new ActualGradeFeedbackDto
        {
            AnalysisId = Result.AnalysisId,
            ActualGrade = grade
        }, CancellationToken.None);
        StatusMessage = $"실제 PSA 등급 {grade}를 저장했습니다.";
    }

    [RelayCommand]
    private async Task ExportJsonAsync()
    {
        if (Result is null)
        {
            return;
        }

        var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"{Result.AnalysisId:N}.json" };
        if (dialog.ShowDialog() == true)
        {
            await _repository.ExportJsonAsync(Result.AnalysisId, dialog.FileName, CancellationToken.None);
            StatusMessage = "JSON을 내보냈습니다.";
        }
    }

    private async Task PrepareDetectionAsync(bool forceAuto = false)
    {
        IsBusy = true;
        try
        {
            if (!string.IsNullOrWhiteSpace(FrontImagePath))
            {
                var corners = forceAuto ? null : FrontCorners;
                FrontPreview = await _previewService.CreateAsync(FrontImagePath, CardSide.Front, corners, null, CancellationToken.None);
                FrontCorners = FrontPreview.Detection.Corners ?? DefaultInset(FrontPreview.Detection.ImageWidth, FrontPreview.Detection.ImageHeight);
                if (FrontPreview.Detection.ImageWidth == 0)
                {
                    FrontCorners = DefaultInset(1000, 1400);
                }
            }

            if (!string.IsNullOrWhiteSpace(BackImagePath))
            {
                var corners = forceAuto ? null : BackCorners;
                BackPreview = await _previewService.CreateAsync(BackImagePath, CardSide.Back, corners, null, CancellationToken.None);
                BackCorners = BackPreview.Detection.Corners ?? DefaultInset(BackPreview.Detection.ImageWidth, BackPreview.Detection.ImageHeight);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunAnalysisAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        ProgressLog.Clear();
        ResetAnalysisStages();
        ApplyProgress(new AnalysisProgress
        {
            Stage = AnalysisStageNames.Prepare,
            Message = "분석을 시작합니다...",
            Percent = 0
        });
        StepIndex = 6;
        try
        {
            var additional = new Dictionary<ImageSlotKind, string>();
            AddSlot(additional, ImageSlotKind.FrontCornerTopLeft, FrontCornerTopLeftPath);
            AddSlot(additional, ImageSlotKind.FrontCornerTopRight, FrontCornerTopRightPath);
            AddSlot(additional, ImageSlotKind.FrontCornerBottomLeft, FrontCornerBottomLeftPath);
            AddSlot(additional, ImageSlotKind.FrontCornerBottomRight, FrontCornerBottomRightPath);
            AddSlot(additional, ImageSlotKind.FrontSurfaceNormal, FrontSurfaceNormalPath);
            AddSlot(additional, ImageSlotKind.FrontSurfaceAngled, FrontSurfaceAngledPath);
            AddSlot(additional, ImageSlotKind.BackSurfaceNormal, BackSurfaceNormalPath);
            AddSlot(additional, ImageSlotKind.BackSurfaceAngled, BackSurfaceAngledPath);

            var input = new CardInput
            {
                CardName = CardName,
                CardNumber = string.IsNullOrWhiteSpace(CardNumber) ? null : CardNumber,
                SetName = string.IsNullOrWhiteSpace(SetName) ? null : SetName,
                Language = string.IsNullOrWhiteSpace(Language) ? null : Language,
                Rarity = string.IsNullOrWhiteSpace(Rarity) ? null : Rarity,
                Memo = string.IsNullOrWhiteSpace(Memo) ? null : Memo,
                FrontImagePath = FrontImagePath,
                BackImagePath = BackImagePath,
                FrontManualCorners = FrontCorners,
                BackManualCorners = BackCorners,
                FrontCenteringGuide = FrontGuide,
                BackCenteringGuide = BackGuide,
                AdditionalImages = additional
            };

            var progress = new Progress<AnalysisProgress>(ApplyProgress);

            Result = await Task.Run(
                async () => await _pipeline.AnalyzeAsync(input, progress, _cts.Token).ConfigureAwait(false),
                _cts.Token);
            await _repository.SaveAsync(Result, _cts.Token);
            StepIndex = 7;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "분석이 취소되었습니다.";
            StepIndex = 5;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StepIndex = 5;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RecalculateRatios()
    {
        if (FrontGuide is not null && FrontPreview?.Centering is not null)
        {
            var m = CenteringMath.FromGuide(FrontGuide, 1260, 1760, FrontPreview.Centering.Confidence);
            FrontRatioText = $"L/R {m.LeftPercent:F1} / {m.RightPercent:F1}    T/B {m.TopPercent:F1} / {m.BottomPercent:F1}";
            LowCenteringConfidence = m.Confidence < 0.55;
        }

        if (BackGuide is not null)
        {
            var confidence = BackPreview?.Centering?.Confidence ?? 0.8;
            var m = CenteringMath.FromGuide(BackGuide, 1260, 1760, confidence);
            BackRatioText = $"L/R {m.LeftPercent:F1} / {m.RightPercent:F1}    T/B {m.TopPercent:F1} / {m.BottomPercent:F1}";
        }
    }

    private static QuadCorners DefaultInset(double width, double height)
    {
        width = width <= 0 ? 1000 : width;
        height = height <= 0 ? 1400 : height;
        var x = width * 0.08;
        var y = height * 0.08;
        return new QuadCorners
        {
            TopLeft = new ImagePoint(x, y),
            TopRight = new ImagePoint(width - x, y),
            BottomRight = new ImagePoint(width - x, height - y),
            BottomLeft = new ImagePoint(x, height - y)
        };
    }

    private static void AddSlot(IDictionary<ImageSlotKind, string> slots, ImageSlotKind kind, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            slots[kind] = path;
        }
    }

    private void ResetAnalysisStages()
    {
        AnalysisStages.Clear();
        foreach (var name in AnalysisStageNames.All)
        {
            AnalysisStages.Add(new AnalysisStageItem(name));
        }
    }

    private void ApplyProgress(AnalysisProgress progress)
    {
        ProgressPercent = Math.Clamp(progress.Percent, 0, 100);
        ProgressPercentText = $"{ProgressPercent:F0}%";
        ProgressStage = progress.Stage;
        ProgressMessage = progress.Message;
        ProgressBarLabel = string.IsNullOrWhiteSpace(progress.Stage)
            ? ProgressPercentText
            : $"{progress.Stage}  ·  {ProgressPercentText}";
        StatusMessage = string.IsNullOrWhiteSpace(progress.Message) ? progress.Stage : progress.Message;
        ProgressLog.Insert(0, $"{ProgressPercentText}  {progress.Stage}: {progress.Message}");

        var foundCurrent = false;
        foreach (var stage in AnalysisStages)
        {
            if (stage.Name == progress.Stage)
            {
                stage.IsActive = progress.Percent < 100;
                stage.IsCompleted = progress.Percent >= 100;
                foundCurrent = true;
            }
            else if (!foundCurrent)
            {
                stage.IsActive = false;
                stage.IsCompleted = true;
            }
            else
            {
                stage.IsActive = false;
                stage.IsCompleted = progress.Percent >= 100;
            }
        }
    }
}
