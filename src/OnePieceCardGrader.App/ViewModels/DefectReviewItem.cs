using CommunityToolkit.Mvvm.ComponentModel;
using OnePieceCardGrader.Core.Enums;
using OnePieceCardGrader.Core.Models;

namespace OnePieceCardGrader.App.ViewModels;

public partial class DefectReviewItem : ObservableObject
{
    public DefectReviewItem(DetectedDefect defect)
    {
        Defect = defect;
        Title = string.IsNullOrWhiteSpace(defect.Title) ? defect.Type.ToString() : defect.Title;
        Explanation = defect.Description;
        Impact = defect.Impact;
        ZoomImagePath = defect.ZoomImagePath;
        SeverityText = defect.Severity switch
        {
            DefectSeverity.Severe => "매우 심함",
            DefectSeverity.Major => "심함",
            DefectSeverity.Moderate => "뚜렷함",
            DefectSeverity.Minor => "약함",
            DefectSeverity.Trace => "아주 약함",
            _ => ""
        };
        _isCounted = defect.ReviewStatus is not DefectReviewStatus.Ignored
                     and not DefectReviewStatus.FalsePositive;
    }

    public DetectedDefect Defect { get; }
    public string Title { get; }
    public string Explanation { get; }
    public string Impact { get; }
    public string? ZoomImagePath { get; }
    public string SeverityText { get; }
    public bool HasZoom => !string.IsNullOrWhiteSpace(ZoomImagePath);

    [ObservableProperty] private bool _isCounted;
}
