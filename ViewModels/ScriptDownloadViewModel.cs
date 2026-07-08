using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamLuaManager.Services;

namespace SteamLuaManager.ViewModels;

public partial class ScriptDownloadViewModel : ObservableObject
{
    private readonly ISteamPathService _steamPathService;

    [ObservableProperty]
    private string _gameId = string.Empty;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    public ObservableCollection<FoundGame> SearchResults { get; } = new();

    public ObservableCollection<string> LogLines { get; } = new();

    public ScriptDownloadViewModel(ISteamPathService steamPathService)
    {
        _steamPathService = steamPathService;
    }

    public record FoundGame(int AppId, string Name, string CoverUrl);

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (IsSearching) return;

        var query = GameId?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            StatusMessage = "请输入游戏ID或名称";
            return;
        }

        IsSearching = true;
        SearchResults.Clear();
        LogLines.Clear();
        AddLog($"🔍 搜索：{query}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(7));

        try
        {
            if (int.TryParse(query, out int appId))
            {
                var name = await GetAppNameAsync(appId, cts.Token);
                if (name != null)
                {
                    SearchResults.Add(new FoundGame(appId, name,
                        $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg"));
                    AddLog($"✅ 找到：{name} (ID: {appId})");
                    StatusMessage = $"找到：{name}";
                }
                else
                {
                    AddLog($"❌ 未找到 AppId 对应的游戏：{appId}");
                    StatusMessage = "未找到匹配的游戏";
                }
            }
            else
            {
                await SearchByNameAsync(query, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            AddLog("❌ 搜索超时（7秒），请检查网络连接");
            AddLog("💡 建议：尝试开启VPN或代理后重试");
            StatusMessage = "搜索超时，请检查网络";
        }
        catch (Exception ex)
        {
            AddLog($"❌ 搜索异常：{ex.Message}");
            StatusMessage = $"搜索异常：{ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async Task<string?> GetAppNameAsync(int appId, CancellationToken ct = default)
    {
        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l=zh-cn";
            var json = await httpClient.GetStringAsync(url, ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement.GetProperty(appId.ToString());
            if (root.TryGetProperty("data", out var data) && data.TryGetProperty("name", out var name))
            {
                return name.GetString();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Silently fail, fallback to "App {appId}"
        }
        return null;
    }

    private async Task SearchByNameAsync(string name, CancellationToken ct)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
        httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        var url = $"https://store.steampowered.com/api/storesearch/?term={Uri.EscapeDataString(name)}&cc=us&l=zh-cn";
        var json = await httpClient.GetStringAsync(url, ct);

        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");

        if (items.GetArrayLength() == 0)
        {
            AddLog("❌ 未找到匹配的游戏");
            StatusMessage = "未找到匹配的游戏";
            return;
        }

        int count = Math.Min(items.GetArrayLength(), 10);
        for (int i = 0; i < count; i++)
        {
            var item = items[i];
            var appId = item.GetProperty("id").GetInt32();
            var gameName = item.GetProperty("name").GetString() ?? name;

            string coverUrl;
            if (item.TryGetProperty("large_image", out var largeImg) && !string.IsNullOrEmpty(largeImg.GetString()))
            {
                coverUrl = largeImg.GetString()!;
            }
            else if (item.TryGetProperty("small_image", out var smallImg) && !string.IsNullOrEmpty(smallImg.GetString()))
            {
                coverUrl = smallImg.GetString()!;
            }
            else
            {
                coverUrl = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg";
            }

            SearchResults.Add(new FoundGame(appId, gameName, coverUrl));
        }

        AddLog($"✅ 找到 {count} 个匹配结果");
        StatusMessage = $"找到 {count} 个匹配结果";
    }

    [RelayCommand]
    private async Task DownloadGameAsync(FoundGame game)
    {
        if (game == null || IsDownloading) return;
        await ExecuteDownloadAsync(game.AppId.ToString());
    }

    private async Task ExecuteDownloadAsync(string gameId)
    {
        IsDownloading = true;
        LogLines.Clear();
        AddLog($"🚀 开始处理 ID：{gameId}");
        AddLog("=".PadRight(50, '='));

        try
        {
            var luaFolder = _steamPathService.GetLuaFolder();
            if (string.IsNullOrEmpty(luaFolder))
            {
                AddLog("❌ 未配置 Steam 路径，请先在基本设置中设置路径");
                StatusMessage = "未配置 Steam 路径";
                return;
            }

            if (!Directory.Exists(luaFolder))
            {
                Directory.CreateDirectory(luaFolder);
                AddLog($"📂 已创建目录：{luaFolder}");
            }
            else
            {
                AddLog($"📂 目标目录：{luaFolder}");
            }

            using var httpClient = new HttpClient();

            AddLog("🔗 获取下载地址...");
            var shortCode = await GetShortCodeAsync(httpClient, gameId);
            if (string.IsNullOrEmpty(shortCode))
            {
                AddLog("❌ 获取短码失败");
                StatusMessage = "获取短码失败";
                return;
            }
            AddLog($"🔗 获取短码：{shortCode}");

            AddLog("📥 开始下载文件...");
            var zipPath = Path.Combine(Path.GetTempPath(), $"{gameId}.zip");
            var success = await DownloadFileAsync(httpClient, shortCode, zipPath);
            if (!success)
            {
                AddLog("❌ 下载失败");
                StatusMessage = "下载失败";
                return;
            }
            AddLog("✅ 文件下载完成");

            AddLog("📦 正在解压...");
            var luaCount = ExtractLuaFiles(zipPath, luaFolder);
            AddLog("✅ 清理临时压缩包");

            if (luaCount > 0)
            {
                AddLog($"🎉 入库完成！共导入 {luaCount} 个 Lua 脚本");
                StatusMessage = $"成功入库 {luaCount} 个 Lua 脚本";
            }
            else
            {
                AddLog("⚠️ 未找到任何 Lua 文件");
                StatusMessage = "未找到 Lua 文件";
            }
        }
        catch (Exception ex)
        {
            AddLog($"❌ 任务异常：{ex.Message}");
            StatusMessage = $"异常：{ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    private async Task<string?> GetShortCodeAsync(HttpClient httpClient, string gameId)
    {
        var targetUrl = $"https://steamgames554.s3.us-east-1.amazonaws.com/{gameId}.zip";
        var payload = new Dictionary<string, string> { { "url", targetUrl } };

        return await RetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://short.walftech.com/api_create_link.php")
            {
                Content = new FormUrlEncodedContent(payload)
            };
            request.Headers.TryAddWithoutValidation("content-type", "application/x-www-form-urlencoded");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"148\", \"Microsoft Edge\";v=\"148\", \"Not/A)Brand\";v=\"99\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("accept", "*/*");
            request.Headers.TryAddWithoutValidation("origin", "https://remlua.com");
            request.Headers.TryAddWithoutValidation("referer", "https://remlua.com/");

            using var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("short_code").GetString();
            }
            catch
            {
                return null;
            }
        }, "获取短码");
    }

    private async Task<bool> DownloadFileAsync(HttpClient httpClient, string shortCode, string savePath)
    {
        var proxyUrl = $"https://short.walftech.com/proxy.php?short={shortCode}";
        return await RetryAsync(async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, proxyUrl);
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Chromium\";v=\"148\", \"Microsoft Edge\";v=\"148\", \"Not/A)Brand\";v=\"99\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("upgrade-insecure-requests", "1");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
            request.Headers.TryAddWithoutValidation("sec-fetch-user", "?1");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
            request.Headers.TryAddWithoutValidation("referer", $"https://short.walftech.com/?id={shortCode}");
            request.Headers.TryAddWithoutValidation("accept-language", "zh-CN,zh;q=0.9");
            request.Headers.TryAddWithoutValidation("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/148.0.0.0 Safari/537.36 Edg/148.0.0.0");

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            await contentStream.CopyToAsync(fileStream);
            fileStream.Flush(true);
            return true;
        }, "下载文件");
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> action, string stepName, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                AddLog($"⚠️ {stepName}失败（第{attempt}次），{delay.TotalSeconds}s后重试...");
                await Task.Delay(delay);
            }
        }
        AddLog($"❌ {stepName}失败，已重试{maxRetries}次");
        throw new HttpRequestException($"{stepName}失败，请检查网络后重试");
    }

    private bool IsTransientError(HttpRequestException ex)
    {
        var statusCode = ex.StatusCode;
        return statusCode == System.Net.HttpStatusCode.BadGateway
            || statusCode == System.Net.HttpStatusCode.ServiceUnavailable
            || statusCode == System.Net.HttpStatusCode.GatewayTimeout
            || statusCode == null;
    }

    private int ExtractLuaFiles(string zipPath, string targetDir)
    {
        int count = 0;
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
                {
                    var destPath = Path.Combine(targetDir, Path.GetFileName(entry.FullName));
                    entry.ExtractToFile(destPath, overwrite: true);
                    count++;
                    AddLog($"📄 导入：{Path.GetFileName(entry.FullName)}");
                }
            }
        }
        File.Delete(zipPath);
        return count;
    }

    private void AddLog(string message)
    {
        Application.Current.Dispatcher.Invoke(() => LogLines.Add(message));
    }

    [RelayCommand]
    private void ClearLog()
    {
        LogLines.Clear();
        SearchResults.Clear();
        StatusMessage = "日志已清除";
    }
}
