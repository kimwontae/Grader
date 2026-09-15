using System.Text.Json;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Options;
using OnePieceCardGrader.Infrastructure.FileStorage;

namespace OnePieceCardGrader.Infrastructure.Settings;

public sealed class JsonAppSettingsStore : IAppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExpertAnalysisSettings Load()
    {
        AppPaths.EnsureCreated();
        if (!File.Exists(AppPaths.SettingsPath))
        {
            var created = new ExpertAnalysisSettings();
            Save(created);
            return created;
        }

        var json = File.ReadAllText(AppPaths.SettingsPath);
        return JsonSerializer.Deserialize<ExpertAnalysisSettings>(json, JsonOptions) ?? new ExpertAnalysisSettings();
    }

    public void Save(ExpertAnalysisSettings settings)
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(AppPaths.SettingsPath, json);
    }
}
