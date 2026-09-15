using OnePieceCardGrader.Core.Constants;

namespace OnePieceCardGrader.Infrastructure.FileStorage;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppConstants.ApplicationFolderName);

    public static string DataRoot => Path.Combine(Root, "Data");
    public static string ImagesRoot => Path.Combine(DataRoot, "Images");
    public static string DatabaseDirectory => Path.Combine(DataRoot, "Database");
    public static string DatabasePath => Path.Combine(DatabaseDirectory, AppConstants.DatabaseFileName);
    public static string SettingsPath => Path.Combine(DataRoot, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(ImagesRoot);
        Directory.CreateDirectory(DatabaseDirectory);
    }
}
