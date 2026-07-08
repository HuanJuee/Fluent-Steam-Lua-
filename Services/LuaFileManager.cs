using System.IO;
using SteamLuaManager.Models;

namespace SteamLuaManager.Services;

public class LuaFileManager : ILuaFileManager, IDisposable
{
    private readonly ISteamPathService _steamPathService;
    private FileSystemWatcher? _watcher;
    private bool _isWatching;
    private CancellationTokenSource? _debounceCts;

    public event EventHandler? FilesChanged;

    public LuaFileManager(ISteamPathService steamPathService)
    {
        _steamPathService = steamPathService;
    }

    public async Task<List<GameInfo>> ScanLuaFilesAsync()
    {
        return await Task.Run(() =>
        {
            var result = new List<GameInfo>();
            var luaFolder = _steamPathService.GetLuaFolder();
            if (string.IsNullOrEmpty(luaFolder) || !Directory.Exists(luaFolder))
                return result;

            var luaFiles = Directory.GetFiles(luaFolder, "*.lua");
            foreach (var file in luaFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (int.TryParse(fileName, out var appId))
                {
                    result.Add(new GameInfo
                    {
                        AppId = appId,
                        LuaFilePath = file
                    });
                }
            }

            return result;
        });
    }

    public async Task AddLuaFileAsync(string sourceFilePath)
    {
        var luaFolder = _steamPathService.GetLuaFolder();
        if (string.IsNullOrEmpty(luaFolder)) return;

        var fileName = Path.GetFileName(sourceFilePath);
        var destPath = Path.Combine(luaFolder, fileName);

        await Task.Run(() => File.Copy(sourceFilePath, destPath, true));
    }

    public async Task DeleteLuaFileAsync(int appId)
    {
        var luaFolder = _steamPathService.GetLuaFolder();
        if (string.IsNullOrEmpty(luaFolder)) return;

        var filePath = Path.Combine(luaFolder, $"{appId}.lua");
        if (File.Exists(filePath))
        {
            await Task.Run(() => File.Delete(filePath));
        }
    }

    public void StartWatching()
    {
        if (_isWatching) return;

        var luaFolder = _steamPathService.GetLuaFolder();
        if (string.IsNullOrEmpty(luaFolder) || !Directory.Exists(luaFolder)) return;

        _watcher = new FileSystemWatcher(luaFolder, "*.lua")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.Size
        };

        _watcher.Created += OnFilesChanged;
        _watcher.Changed += OnFilesChanged;
        _watcher.Deleted += OnFilesChanged;
        _watcher.Renamed += OnFilesChanged;
        _watcher.EnableRaisingEvents = true;
        _isWatching = true;
    }

    public void StopWatching()
    {
        if (!_isWatching || _watcher == null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _watcher = null;
        _isWatching = false;
    }

    private void OnFilesChanged(object sender, FileSystemEventArgs e)
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, token);
                if (!token.IsCancellationRequested)
                    FilesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (OperationCanceledException) { }
        }, token);
    }

    public void Dispose()
    {
        StopWatching();
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
