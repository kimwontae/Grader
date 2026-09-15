using CommunityToolkit.Mvvm.ComponentModel;

namespace OnePieceCardGrader.App.ViewModels;

public partial class AnalysisStageItem : ObservableObject
{
    public AnalysisStageItem(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private bool _isCompleted;

    public string StatusText => IsActive ? "진행 중" : IsCompleted ? "완료" : "대기";

    partial void OnIsActiveChanged(bool value) => OnPropertyChanged(nameof(StatusText));
    partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(StatusText));
}
