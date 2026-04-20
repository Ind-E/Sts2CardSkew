using System.Text.Json;
using Godot;

namespace BalatroEffects;

public static class Config
{
    private const int CurrentVersion = 1;

    private static readonly string FolderPath = Path.Combine(OS.GetUserDataDir(), "mod_configs");
    private static readonly string FilePath = Path.Combine(FolderPath, "BalatroEffectsConfig.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private class ConfigData
    {
        public int Version { get; set; } = CurrentVersion;
        public Dictionary<string, int> EffectSettings { get; set; } = [];
        public Dictionary<int, double> IntensitySettings { get; set; } = [];
    }

    public static Dictionary<string, int> EffectSettings { get; private set; } = [];
    public static Dictionary<int, double> IntensitySettings { get; private set; } = [];

    public static void Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                Save();
                return;
            }

            string json = File.ReadAllText(FilePath);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("Version", out JsonElement versionElement))
            {
                MainFile.Logger.Info("V0 config detected. Migrating to V" + CurrentVersion);
                var oldData = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                EffectSettings = oldData ?? [];
                IntensitySettings = [];

                Save();
                return;
            }

            int fileVersion = versionElement.GetInt32();
            if (fileVersion < CurrentVersion)
            {
                MainFile.Logger.Info("???");
            }
            else if (JsonSerializer.Deserialize<ConfigData>(json) is ConfigData data)
            {
                EffectSettings = data.EffectSettings ?? [];
                IntensitySettings = data.IntensitySettings ?? [];
            }
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"Failed to load config: {e.Message}");
        }
    }

    public static void Save()
    {
        try
        {
            if (!Directory.Exists(FolderPath))
            {
                Directory.CreateDirectory(FolderPath);
            }

            var data = new ConfigData
            {
                Version = CurrentVersion,
                EffectSettings = EffectSettings,
                IntensitySettings = IntensitySettings,
            };

            string json = JsonSerializer.Serialize(data, Options);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            MainFile.Logger.Error($"Failed to save config: {e.Message}");
        }
    }

    public static int GetEffect(string cardId)
    {
        return EffectSettings.TryGetValue(cardId, out int val) ? val : 0;
    }

    public static void SetEffect(string cardId, int index)
    {
        EffectSettings[cardId] = index;
        Save();
    }

    public static double GetIntensity(int effectIndex, double defaultValue = 1.0f)
    {
        return IntensitySettings.TryGetValue(effectIndex, out double val) ? val : defaultValue;
    }

    public static void SetIntensity(int effectIndex, double value)
    {
        IntensitySettings[effectIndex] = Math.Clamp(value, 0.0f, 1.0f);
        Save();
    }
}
