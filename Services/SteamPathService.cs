using System.IO;
using Microsoft.Win32;

namespace SteamLuaManager.Services;

public class SteamPathService : ISteamPathService
{
    private const string RegistryPath = @"SOFTWARE\WOW6432Node\Valve\Steam";
    private const string InstallPathKey = "InstallPath";
    private const string LuaSubFolder = @"config\lua";
    private string? _customPath;

    public string? DetectSteamPath()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryPath);
            if (key?.GetValue(InstallPathKey) is string installPath)
            {
                if (File.Exists(Path.Combine(installPath, "steam.exe")))
                    return installPath;
            }
        }
        catch
        {
        }

        string[] commonPaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"D:\Program Files (x86)\Steam",
            @"E:\Steam",
            @"E:\Program Files (x86)\Steam"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(Path.Combine(path, "steam.exe")))
                return path;
        }

        return null;
    }

    public string? GetLuaFolder()
    {
        var basePath = !string.IsNullOrEmpty(_customPath) ? _customPath : DetectSteamPath();
        if (string.IsNullOrEmpty(basePath)) return null;

        var luaPath = Path.Combine(basePath, LuaSubFolder);
        if (!Directory.Exists(luaPath))
        {
            try { Directory.CreateDirectory(luaPath); }
            catch { return null; }
        }
        return luaPath;
    }

    public void SetCustomPath(string path) => _customPath = path;
    public string? GetCustomPath() => _customPath;

    public SteamToolType DetectSteamToolType()
    {
        var steamPath = !string.IsNullOrEmpty(_customPath) ? _customPath : DetectSteamPath();
        if (string.IsNullOrEmpty(steamPath)) return SteamToolType.None;

        // OpenSteamTool (开源) — 独有标识
        if (File.Exists(Path.Combine(steamPath, "OpenSteamTool.dll")) ||
            File.Exists(Path.Combine(steamPath, "opensteamtool.toml")))
            return SteamToolType.OpenSteamTool;

        // SteamTools (闭源) — 独有标识
        if (File.Exists(Path.Combine(steamPath, "hid.dll")) ||
            File.Exists(Path.Combine(steamPath, "steam.cfg")) ||
            Directory.Exists(Path.Combine(steamPath, @"config\stplug-in")))
            return SteamToolType.SteamTools;

        return SteamToolType.None;
    }
}
