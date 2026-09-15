using System.IO;
using System.Windows.Media.Imaging;

namespace OnePieceCardGrader.App.Services;

public static class BitmapLoader
{
    public static BitmapImage? Load(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static (double Width, double Height) GetSize(string? path)
    {
        var image = Load(path);
        return image is null ? (0, 0) : (image.PixelWidth, image.PixelHeight);
    }
}
