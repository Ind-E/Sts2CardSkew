using System.Text.Json;
using Godot;

namespace BalatroEffects;

public static class Config
{
    private static readonly string FolderPath = Path.Combine(OS.GetUserDataDir(), "mod_configs");
    private static readonly string FilePath = Path.Combine(FolderPath, "BalatroEffectsConfig.json");
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static Dictionary<string, int> CardSettings { get; private set; } = new();

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
            var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
            if (data != null)
            {
                CardSettings = data;
            }
        }
        catch (System.Exception e)
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

            string json = JsonSerializer.Serialize(CardSettings, Options);
            File.WriteAllText(FilePath, json);
        }
        catch (System.Exception e)
        {
            MainFile.Logger.Error($"Failed to save config: {e.Message}");
        }
    }

    public static int GetIndex(string cardId)
    {
        return CardSettings.TryGetValue(cardId, out int val) ? val : 0;
    }

    public static void SetIndex(string cardId, int index)
    {
        CardSettings[cardId] = index;
        Save();
    }
}
