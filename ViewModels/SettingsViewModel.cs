using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SteamLuaManager.Models;
using SteamLuaManager.Services;

namespace SteamLuaManager.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISteamPathService _steamPathService;
    private readonly ILuaFileManager _luaFileManager;
    private readonly ISettingsService _settingsService;
    private readonly ISteamApiService _steamApiService;
    private AppSettings _settings;

    [ObservableProperty]
    private string _steamPath = string.Empty;

    [ObservableProperty]
    private bool _isAutoRefreshEnabled = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private int _selectedCdnIndex;

    [ObservableProperty]
    private bool _isSpeedTesting;

    [ObservableProperty]
    private string _selectedBackdrop = "Acrylic10";

    public record BackdropOption(string Display, string Value);

    public List<BackdropOption> BackdropOptions { get; } = new()
    {
        new("亚克力", "Acrylic10"),
        new("云母", "Mica"),
        new("无", "None"),
    };

    public List<CdnEndpoint> CdnEndpoints { get; } = CdnEndpoint.Defaults;

    public ObservableCollection<SpeedTestItem> SpeedTestResults { get; } = new();

    public SettingsViewModel(ISteamPathService steamPathService, ILuaFileManager luaFileManager,
        ISettingsService settingsService, ISteamApiService steamApiService)
    {
        _steamPathService = steamPathService;
        _luaFileManager = luaFileManager;
        _settingsService = settingsService;
        _steamApiService = steamApiService;
        _settings = settingsService.Load();

        SteamPath = _settings.SteamPath;
        IsAutoRefreshEnabled = _settings.AutoRefreshEnabled;
        SelectedCdnIndex = _settings.SelectedCdnIndex;
        _selectedBackdrop = _settings.SelectedBackdrop;

        if (string.IsNullOrEmpty(SteamPath))
        {
            var detectedPath = steamPathService.DetectSteamPath();
            SteamPath = detectedPath ?? "未检测到Steam";
        }
    }

    private DispatcherTimer? _statusTimer;

    partial void OnStatusMessageChanged(string value)
    {
        _statusTimer?.Stop();
        if (!string.IsNullOrEmpty(value))
        {
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += (s, e) =>
            {
                _statusTimer?.Stop();
                StatusMessage = string.Empty;
            };
            _statusTimer.Start();
        }
    }

    partial void OnSelectedCdnIndexChanged(int value)
    {
        _settings.SelectedCdnIndex = value;
        _settingsService.Save(_settings);
        _steamApiService.UpdateCdnPreference(value);
        StatusMessage = $"封面节点已切换: {CdnEndpoints[value].Name}";
    }

    partial void OnIsAutoRefreshEnabledChanged(bool value)
    {
        _settings.AutoRefreshEnabled = value;
        _settingsService.Save(_settings);

        if (value)
            _luaFileManager.StartWatching();
        else
            _luaFileManager.StopWatching();
        StatusMessage = value ? "自动监控已开启" : "自动监控已关闭";
    }

    partial void OnSelectedBackdropChanged(string value)
    {
        _settings.SelectedBackdrop = value;
        _settingsService.Save(_settings);
        StatusMessage = value switch
        {
            "Acrylic10" => "背景效果已切换为亚克力",
            "Mica" => "背景效果已切换为云母",
            "None" => "背景效果已关闭",
            _ => ""
        };
    }

    [RelayCommand]
    private void BrowseSteamPath()
    {
        var dialog = new OpenFileDialog
        {
            FileName = "steam.exe",
            Filter = "Steam可执行文件|steam.exe",
            Title = "选择Steam安装路径"
        };
        if (dialog.ShowDialog() == true)
        {
            var dir = Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(dir))
            {
                SteamPath = dir;
                _steamPathService.SetCustomPath(dir);
                _settings.SteamPath = dir;
                _settingsService.Save(_settings);
                StatusMessage = $"Steam路径已设置为: {dir}";
            }
        }
    }

    [RelayCommand]
    private void ResetSteamPath()
    {
        _steamPathService.SetCustomPath(string.Empty);
        var detectedPath = _steamPathService.DetectSteamPath();
        SteamPath = detectedPath ?? "未检测到Steam";
        _settings.SteamPath = string.Empty;
        _settingsService.Save(_settings);
        StatusMessage = "已重置为自动检测路径";
    }

    [RelayCommand]
    private async Task ClearCacheAsync()
    {
        try
        {
            var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
            if (!Directory.Exists(cacheDir))
            {
                StatusMessage = "没有需要清理的缓存";
                return;
            }

            var lockedFiles = new List<string>();
            var deletedCount = 0;

            await Task.Run(() =>
            {
                foreach (var file in Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                    }
                    catch (IOException)
                    {
                        lockedFiles.Add(Path.GetFileName(file));
                    }
                    catch { }
                }

                foreach (var dir in Directory.GetDirectories(cacheDir))
                {
                    try { Directory.Delete(dir, true); }
                    catch { }
                }

                Directory.CreateDirectory(Path.Combine(cacheDir, "covers"));
            });

            if (lockedFiles.Count > 0)
                StatusMessage = $"缓存已清理(跳过{lockedFiles.Count}个占用文件)";
            else
                StatusMessage = $"缓存已清理(共{deletedCount}个文件)";
        }
        catch (Exception ex) { StatusMessage = $"清理失败: {ex.Message}"; }
    }

    [RelayCommand]
    private void OpenLuaFolder()
    {
        var luaFolder = _steamPathService.GetLuaFolder();
        if (!string.IsNullOrEmpty(luaFolder) && Directory.Exists(luaFolder))
            Process.Start(new ProcessStartInfo { FileName = luaFolder, UseShellExecute = true });
        else StatusMessage = "Lua文件夹不存在";
    }

    [RelayCommand]
    private void OpenCacheFolder()
    {
        var cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
        if (!Directory.Exists(cacheDir))
            Directory.CreateDirectory(cacheDir);
        Process.Start(new ProcessStartInfo { FileName = cacheDir, UseShellExecute = true });
    }

    [RelayCommand]
    private async Task TestCdnSpeedAsync()
    {
        if (IsSpeedTesting) return;
        IsSpeedTesting = true;
        SpeedTestResults.Clear();
        StatusMessage = "正在测试所有CDN节点...";

        try
        {
            var results = await _steamApiService.TestCdnSpeedAsync();
            SpeedTestResults.Clear();
            foreach (var (name, latency, success) in results)
                SpeedTestResults.Add(new SpeedTestItem { Name = name, LatencyMs = latency, IsSuccess = success });

            var best = SpeedTestResults.Where(r => r.IsSuccess).OrderBy(r => r.LatencyMs).FirstOrDefault();
            if (best != null)
                StatusMessage = $"测速完成，最快节点: {best.Name} ({best.LatencyMs}ms)";
            else
                StatusMessage = "所有节点均不可达";
        }
        catch (Exception ex)
        {
            StatusMessage = $"测速失败: {ex.Message}";
        }
        finally
        {
            IsSpeedTesting = false;
        }
    }

    [ObservableProperty]
    private string _selectedSettingsCategory = "Basic";

    [RelayCommand]
    private void SwitchSettingsCategory(string category)
    {
        SelectedSettingsCategory = category;
    }
}

public class SpeedTestItem
{
    public string Name { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public bool IsSuccess { get; set; }
    public string StatusText => IsSuccess ? $"{LatencyMs}ms" : "失败";
    public string ColorCode => IsSuccess ? LatencyMs switch
    {
        <= 200 => "#4CAF50",
        <= 500 => "#FF9800",
        _ => "#F44336"
    } : "#F44336";
}