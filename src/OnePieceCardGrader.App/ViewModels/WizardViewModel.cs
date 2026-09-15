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
    }

    public string Disclaimer { get; }
    public string ConfidenceMeaning { get; }
    public ObservableCollection<string> ProgressLog { get; } = [];

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
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = "카드 정보를 입력한 뒤 Front/Back 사진을 업로드하세요.";
    [ObservableProperty] private double _progressPercent;
    [ObservableProperty] private string _progressStage = string.Empty;
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
        4 => "5. 분석",
        _ => "6. 결과"
    };

    public bool CanGoNext => StepIndex is >= 0 and < 5;
    public bool CanGoBack => StepIndex > 0 && StepIndex != 4;
    public bool IsInfoStep => StepIndex == 0;
    public bool IsPhotoStep => StepIndex == 1;
    public bool IsDetectionStep => StepIndex == 2;
    public bool IsCenteringStep => StepIndex == 3;
    public bool IsProgressStep => StepIndex == 4;
    public bool IsResultStep => StepIndex == 5;

    partial void OnStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsInfoStep));
        OnPropertyChanged(nameof(IsPhotoStep));
        OnPropertyChanged(nameof(IsDetectionStep));
        OnPropertyChanged(nameof(IsCenteringStep));
        OnPropertyChanged(nameof(IsProgressStep));
        OnPropertyChanged(nameof(IsResultStep));
    }

    partial void OnFrontGuideChanged(CenteringGuide? value) => RecalculateRatios();
    partial void OnBackGuideChanged(CenteringGuide? value) => RecalculateRatios();
    partial void OnFrontPreviewChanged(CardPreviewResult? value) => RecalculateRatios();
    partial void OnBackPreviewChanged(CardPreviewResult? value) => RecalculateRatios();

    [RelayCommand]
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

        if (StepIndex == 3)
        {
            await RunAnalysisAsync();
            return;
        }

        if (StepIndex < 5)
        {
            StepIndex++;
        }
    }

    [RelayCommand]
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
        StepIndex = 3;
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
        StepIndex = 4;
        try
        {
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
                BackCenteringGuide = BackGuide
            };

            var progress = new Progress<AnalysisProgress>(p =>
            {
                ProgressPercent = p.Percent;
                ProgressStage = p.Stage;
                StatusMessage = p.Message;
                ProgressLog.Insert(0, $"{p.Stage}: {p.Message}");
            });

            Result = await _pipeline.AnalyzeAsync(input, progress, _cts.Token);
            await _repository.SaveAsync(Result, _cts.Token);
            StepIndex = 5;
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "분석이 취소되었습니다.";
            StepIndex = 3;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            StepIndex = 3;
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
}
