using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace OnePieceCardGrader.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public MainViewModel(IServiceProvider services)
    {
        _services = services;
        CurrentContent = _services.GetRequiredService<WizardViewModel>();
    }

    [ObservableProperty]
    private ObservableObject _currentContent;

    [ObservableProperty]
    private string _currentSection = "NewAnalysis";

    [RelayCommand]
    private void Navigate(string section)
    {
        CurrentSection = section;
        CurrentContent = section switch
        {
            "History" => _services.GetRequiredService<HistoryViewModel>(),
            "Standards" => _services.GetRequiredService<StandardsViewModel>(),
            "Settings" => _services.GetRequiredService<SettingsViewModel>(),
            "About" => _services.GetRequiredService<AboutViewModel>(),
            _ => _services.GetRequiredService<WizardViewModel>()
        };
    }
}
