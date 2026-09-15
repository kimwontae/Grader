using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OnePieceCardGrader.Core.DTOs;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Infrastructure.FileStorage;

namespace OnePieceCardGrader.App.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    private readonly IAnalysisRepository _repository;

    public HistoryViewModel(IAnalysisRepository repository)
    {
        _repository = repository;
        _ = LoadAsync();
    }

    public ObservableCollection<AnalysisListItemDto> Items { get; } = [];

    [ObservableProperty] private AnalysisListItemDto? _selected;
    [ObservableProperty] private string _actualGradeText = string.Empty;
    [ObservableProperty] private string _certNumber = string.Empty;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    [RelayCommand]
    private async Task LoadAsync()
    {
        Items.Clear();
        var list = await _repository.ListAsync(CancellationToken.None);
        foreach (var item in list)
        {
            Items.Add(item);
        }
    }

    [RelayCommand]
    private async Task SaveActualAsync()
    {
        if (Selected is null || !int.TryParse(ActualGradeText, out var grade))
        {
            Status = "기록을 선택하고 실제 PSA 등급(1-10)을 입력하세요.";
            return;
        }

        await _repository.SaveActualGradeAsync(new ActualGradeFeedbackDto
        {
            AnalysisId = Selected.Id,
            ActualGrade = grade,
            CertNumber = string.IsNullOrWhiteSpace(CertNumber) ? null : CertNumber,
            UserNote = string.IsNullOrWhiteSpace(Note) ? null : Note
        }, CancellationToken.None);
        Status = "실제 PSA 등급을 저장했습니다.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (Selected is null)
        {
            return;
        }

        var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"{Selected.Id:N}.json" };
        if (dialog.ShowDialog() == true)
        {
            await _repository.ExportJsonAsync(Selected.Id, dialog.FileName, CancellationToken.None);
            Status = "JSON export 완료";
        }
    }

    public static string ResolveThumbnail(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return string.Empty;
        }

        return System.IO.Path.IsPathRooted(relative) ? relative : System.IO.Path.Combine(AppPaths.Root, relative);
    }
}
