using CommunityToolkit.Mvvm.ComponentModel;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.App.ViewModels;

public sealed class StandardsViewModel : ObservableObject
{
    public StandardsViewModel(IGradingProfileProvider profiles)
    {
        Profile = profiles.GetProfile("PSA");
    }

    public GradingProfile Profile { get; }
}
