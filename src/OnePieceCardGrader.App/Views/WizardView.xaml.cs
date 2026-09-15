using System.Windows;
using System.Windows.Controls;

namespace OnePieceCardGrader.App.Views;

public partial class WizardView : UserControl
{
    public WizardView()
    {
        InitializeComponent();
    }

    private void OnPhotoGuide(object sender, RoutedEventArgs e)
    {
        var window = new PhotoGuideWindow
        {
            Owner = Window.GetWindow(this)
        };
        window.ShowDialog();
    }
}
