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
}
