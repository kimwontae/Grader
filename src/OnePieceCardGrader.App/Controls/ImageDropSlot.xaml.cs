using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OnePieceCardGrader.App.Services;

namespace OnePieceCardGrader.App.Controls;

public partial class ImageDropSlot : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(ImageDropSlot), new PropertyMetadata("이미지", OnTitleChanged));

    public static readonly DependencyProperty ImagePathProperty =
        DependencyProperty.Register(nameof(ImagePath), typeof(string), typeof(ImageDropSlot), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnPathChanged));

    public ImageDropSlot()
    {
        InitializeComponent();
        MouseLeftButtonUp += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(ImagePath))
            {
                OnBrowse(this, new RoutedEventArgs());
            }
        };
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? ImagePath
    {
        get => (string?)GetValue(ImagePathProperty);
        set => SetValue(ImagePathProperty, value);
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageDropSlot { TitleText: not null } slot)
        {
            slot.TitleText.Text = e.NewValue as string;
        }
    }

    private static void OnPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageDropSlot slot)
        {
            slot.RefreshPreview();
        }
    }

    private void RefreshPreview()
    {
        var image = BitmapLoader.Load(ImagePath);
        if (image is null)
        {
            PreviewHost.Visibility = Visibility.Collapsed;
            Placeholder.Visibility = Visibility.Visible;
            return;
        }

        Preview.Source = image;
        PreviewHost.Visibility = Visibility.Visible;
        Placeholder.Visibility = Visibility.Collapsed;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.tif;*.tiff"
        };
        if (dialog.ShowDialog() == true)
        {
            ImagePath = dialog.FileName;
        }
    }

    private void OnClear(object sender, RoutedEventArgs e) => ImagePath = null;

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0 && File.Exists(files[0]))
        {
            ImagePath = files[0];
        }
    }
}
