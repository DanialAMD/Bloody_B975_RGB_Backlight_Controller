using System.Text.Json;
using B975RgbApp.Models;

namespace B975RgbApp.Services;

internal static class SettingsStore
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "B975RgbApp");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public static LightingSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new LightingSettings();
            }

            return JsonSerializer.Deserialize<LightingSettings>(File.ReadAllText(SettingsPath))
                   ?? new LightingSettings();
        }
        catch
        {
            return new LightingSettings();
        }
    }

    public static void Save(LightingSettings settings)
    {
        Directory.CreateDirectory(SettingsDirectory);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
