using System.IO;
using System.Text.Json;

namespace SteamLuaManager.Services;

public class AppSettings
{
    public string SteamPath { get; set; } = string.Empty;
    public bool AutoRefreshEnabled { get; set; } = true;
    public string CustomLuaPath { get; set; } = string.Empty;
    public int SelectedCdnIndex { get; set; }
    public string SelectedViewMode { get; set; } = "卡片";
    public string SelectedBackdrop { get; set; } = "Acrylic10";
}

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch { }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { }
    }
}
