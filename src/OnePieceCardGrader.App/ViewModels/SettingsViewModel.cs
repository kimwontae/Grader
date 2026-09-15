using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _store;

    public SettingsViewModel(IAppSettingsStore store)
    {
        _store = store;
        Settings = store.Load();
    }

    [ObservableProperty] private ExpertAnalysisSettings _settings;
    [ObservableProperty] private string _status = string.Empty;

    [RelayCommand]
    private void Save()
    {
        _store.Save(Settings);
        Status = "설정을 저장했습니다.";
    }
}
