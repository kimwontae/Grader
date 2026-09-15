using System.Windows;

namespace OnePieceCardGrader.App.Views;

public partial class PhotoGuideWindow : Window
{
    public PhotoGuideWindow()
    {
        InitializeComponent();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
