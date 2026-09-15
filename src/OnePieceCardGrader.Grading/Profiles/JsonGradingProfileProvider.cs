using System.Text.Json;
using OnePieceCardGrader.Core.Interfaces;
using OnePieceCardGrader.Core.Options;

namespace OnePieceCardGrader.Grading.Profiles;

public sealed class JsonGradingProfileProvider : IGradingProfileProvider
{
    private readonly Dictionary<string, GradingProfile> _profiles;

    public JsonGradingProfileProvider(string profilesDirectory)
    {
        _profiles = new Dictionary<string, GradingProfile>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(profilesDirectory))
        {
            _profiles["PSA"] = CreateDefaultPsa();
            return;
        }

        foreach (var file in Directory.GetFiles(profilesDirectory, "*.json"))
        {
            var json = File.ReadAllText(file);
            var profile = JsonSerializer.Deserialize<GradingProfile>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (profile is not null)
            {
                _profiles[profile.Name] = profile;
            }
        }

        if (!_profiles.ContainsKey("PSA"))
        {
            _profiles["PSA"] = CreateDefaultPsa();
        }
    }

    public GradingProfile GetProfile(string name) =>
        _profiles.TryGetValue(name, out var profile) ? profile : _profiles["PSA"];

    public IReadOnlyList<GradingProfile> GetAvailableProfiles() => _profiles.Values.ToArray();

    public static GradingProfile CreateDefaultPsa() => new()
    {
        Name = "PSA",
        Version = "1.0",
        Weights = new GradingWeights(),
        BorderlineDistance = 1.0,
        Centering = new Dictionary<string, CenteringThreshold>
        {
            ["10"] = new() { FrontMax = 55, BackMax = 75 },
            ["9"] = new() { FrontMax = 60, BackMax = 90 },
            ["8"] = new() { FrontMax = 65, BackMax = 90 },
            ["7"] = new() { FrontMax = 70, BackMax = 90 },
            ["6"] = new() { FrontMax = 80, BackMax = 90 },
            ["5"] = new() { FrontMax = 85, BackMax = 95 },
            ["4"] = new() { FrontMax = 90, BackMax = 100 },
            ["3"] = new() { FrontMax = 95, BackMax = 100 },
            ["2"] = new() { FrontMax = 100, BackMax = 100 },
            ["1"] = new() { FrontMax = 100, BackMax = 100 }
        },
        DefectRules = new Dictionary<string, int>
        {
            ["Crease.Severe"] = 5,
            ["Crease.Major"] = 6,
            ["Dent.Major"] = 7,
            ["MissingMaterial.Major"] = 6,
            ["CornerWhitening.Minor"] = 9,
            ["EdgeWhitening.Minor"] = 9,
            ["Scratch.Minor"] = 9
        },
        GradeThresholds = new Dictionary<string, double>
        {
            ["10"] = 96,
            ["9"] = 90,
            ["8"] = 82,
            ["7"] = 74,
            ["6"] = 66,
            ["5"] = 58,
            ["4"] = 48,
            ["3"] = 36,
            ["2"] = 24,
            ["1"] = 0
        }
    };
}
