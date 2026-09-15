using System.Windows;
using OnePieceCardGrader.App.ViewModels;

namespace OnePieceCardGrader.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
