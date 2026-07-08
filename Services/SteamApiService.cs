using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using SteamLuaManager.Models;

namespace SteamLuaManager.Services;

public class SteamApiService : ISteamApiService
{
	private readonly HttpClient _http;
	private readonly HttpClient _coverHttp;
	private readonly string _cacheDir;
	private readonly string _coversDir;
	private readonly string _cacheFilePath;
	private ConcurrentDictionary<int, string> _nameCache = new();
	private readonly SemaphoreSlim _apiGate = new(2, 2);
	private readonly ISettingsService _settingsService;
	private int _selectedCdnIndex;

	public int SelectedCdnIndex => _selectedCdnIndex;

	public SteamApiService(ISettingsService settingsService)
	{
		_settingsService = settingsService;
		_selectedCdnIndex = _settingsService.Load().SelectedCdnIndex;

		_http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
		_http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
		_http.DefaultRequestHeaders.Add("Accept", "application/json");

		_coverHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
		_coverHttp.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

		_cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache");
		_coversDir = Path.Combine(_cacheDir, "covers");
		_cacheFilePath = Path.Combine(_cacheDir, "gameinfo.json");

		Directory.CreateDirectory(_coversDir);
		LoadCache();
	}

	public void UpdateCdnPreference(int selectedIndex)
	{
		_selectedCdnIndex = selectedIndex;
	}

	public async Task<List<(string Name, long LatencyMs, bool IsSuccess)>> TestCdnSpeedAsync()
	{
		const int testAppId = 730;

		using var testClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
		testClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");

		var tasks = CdnEndpoint.Defaults.Select(async cdn =>
		{
			var url = string.Format(cdn.UrlTemplate, testAppId);
			var sw = System.Diagnostics.Stopwatch.StartNew();
			try
			{
				var response = await testClient.GetAsync(url);
				sw.Stop();
				return (cdn.Name, sw.ElapsedMilliseconds, response.IsSuccessStatusCode);
			}
			catch
			{
				sw.Stop();
				return (cdn.Name, sw.ElapsedMilliseconds, false);
			}
		});

		return (await Task.WhenAll(tasks)).ToList();
	}

	private void LoadCache()
	{
		try
		{
			if (File.Exists(_cacheFilePath))
			{
				var json = File.ReadAllText(_cacheFilePath);
				_nameCache = JsonSerializer.Deserialize<ConcurrentDictionary<int, string>>(json) ?? new();
			}
		}
		catch { _nameCache = new(); }
	}

	private void SaveCache()
	{
		try
		{
			var json = JsonSerializer.Serialize(_nameCache, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(_cacheFilePath, json);
		}
		catch { }
	}

	public void PopulateFromCache(List<GameInfo> games)
	{
		foreach (var game in games)
		{
			if (_nameCache.TryGetValue(game.AppId, out var name) && !name.StartsWith("AppID:"))
				game.GameName = name;

			var coverPath = Path.Combine(_coversDir, $"{game.AppId}.jpg");
			if (File.Exists(coverPath) && new FileInfo(coverPath).Length > 1000)
				game.CoverImagePath = coverPath;
		}
	}

	public async Task RefreshGameInfoAsync(List<GameInfo> games, CancellationToken cancellationToken = default)
	{
		var needInfo = games.Where(g =>
			string.IsNullOrEmpty(g.GameName) ||
			g.GameName == $"AppID: {g.AppId}" ||
			string.IsNullOrEmpty(g.CoverImagePath))
			.ToList();

		if (needInfo.Count == 0) return;

		var tasks = needInfo.Select(game => RefreshOneGameAsync(game, cancellationToken));
		await Task.WhenAll(tasks);

		SaveCache();
	}

	private async Task RefreshOneGameAsync(GameInfo game, CancellationToken cancellationToken)
	{
		try
		{
			await _apiGate.WaitAsync(cancellationToken);
		}
		catch
		{
			game.IsLoading = false;
			return;
		}

		try
		{
			game.IsLoading = true;

			var needName = string.IsNullOrEmpty(game.GameName) || game.GameName == $"AppID: {game.AppId}";
			var needCover = string.IsNullOrEmpty(game.CoverImagePath);
			string? headerUrl = null;

			if (needName)
			{
				var storeResult = await TryStoreApi(game.AppId, "schinese", cancellationToken);
				if (storeResult.Name != null)
				{
					game.GameName = storeResult.Name;
					_nameCache[game.AppId] = storeResult.Name;
					headerUrl = storeResult.HeaderUrl;
				}
				else
				{
					storeResult = await TryStoreApi(game.AppId, "english", cancellationToken);
					if (storeResult.Name != null)
					{
						game.GameName = storeResult.Name;
						_nameCache[game.AppId] = storeResult.Name;
						headerUrl = storeResult.HeaderUrl;
					}
					else
					{
						var spyName = await TrySteamSpy(game.AppId, cancellationToken);
						if (spyName != null)
						{
							game.GameName = spyName;
							_nameCache[game.AppId] = spyName;
						}
						else
						{
							var communityName = await TrySteamCommunity(game.AppId, cancellationToken);
							if (communityName != null)
							{
								game.GameName = communityName;
								_nameCache[game.AppId] = communityName;
							}
						}
					}
				}
			}

			if (needCover)
			{
				var cover = await FetchCoverAsync(game.AppId, cancellationToken);
				if (!string.IsNullOrEmpty(cover))
				{
					game.CoverImagePath = cover;
				}
				else if (headerUrl != null)
				{
					cover = await DownloadCoverFromUrl(headerUrl, game.AppId, cancellationToken);
					if (!string.IsNullOrEmpty(cover))
						game.CoverImagePath = cover;
				}
			}
		}
		catch { }
		finally
		{
			game.IsLoading = false;
			_apiGate.Release();
		}
	}

	private async Task<(string? Name, string? HeaderUrl)> TryStoreApi(int appId, string lang, CancellationToken cancellationToken)
	{
		try
		{
			var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&l={lang}&filters=basic";
			using var doc = await JsonDocument.ParseAsync(await _http.GetStreamAsync(url, cancellationToken));
			var root = doc.RootElement;

			if (root.TryGetProperty(appId.ToString(), out var app) &&
				app.TryGetProperty("success", out var ok) && ok.GetBoolean() &&
				app.TryGetProperty("data", out var data))
			{
				var name = data.TryGetProperty("name", out var n) ? n.GetString() : null;
				var header = data.TryGetProperty("header_image", out var h) ? h.GetString() : null;
				return (name, header);
			}
		}
		catch { }
		return (null, null);
	}

	private async Task<string?> TrySteamSpy(int appId, CancellationToken cancellationToken)
	{
		try
		{
			var url = $"https://steamspy.com/api.php?request=appdetails&appid={appId}";
			using var doc = await JsonDocument.ParseAsync(await _http.GetStreamAsync(url, cancellationToken));
			if (doc.RootElement.TryGetProperty("name", out var name))
				return name.GetString();
		}
		catch { }
		return null;
	}

	private async Task<string?> TrySteamCommunity(int appId, CancellationToken cancellationToken)
	{
		try
		{
			var url = $"https://steamcommunity.com/app/{appId}?l=english";
			var html = await _http.GetStringAsync(url, cancellationToken);

			var tag = "<title>";
			var start = html.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
			if (start < 0) return null;
			start += tag.Length;

			var end = html.IndexOf("</title>", start, StringComparison.OrdinalIgnoreCase);
			if (end < 0) return null;

			var title = html[start..end];
			var sep = title.IndexOf(" :: ", StringComparison.OrdinalIgnoreCase);
			if (sep > 0) return title[..sep].Trim();

			sep = title.LastIndexOf(" - ", StringComparison.OrdinalIgnoreCase);
			if (sep > 0) return title[..sep].Trim();

			return title.Trim();
		}
		catch { }
		return null;
	}

	private async Task<string?> DownloadCoverFromUrl(string url, int appId, CancellationToken cancellationToken)
	{
		var localPath = Path.Combine(_coversDir, $"{appId}.jpg");
		if (File.Exists(localPath) && new FileInfo(localPath).Length > 1000)
			return localPath;

		try
		{
			var response = await _coverHttp.GetAsync(url, cancellationToken);
			if (!response.IsSuccessStatusCode) return null;

			var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
			if (bytes.Length <= 1000) return null;

			await File.WriteAllBytesAsync(localPath, bytes, cancellationToken);
			return localPath;
		}
		catch { }
		return null;
	}

	private async Task<string?> FetchCoverAsync(int appId, CancellationToken cancellationToken)
	{
		var localPath = Path.Combine(_coversDir, $"{appId}.jpg");
		if (File.Exists(localPath) && new FileInfo(localPath).Length > 1000)
			return localPath;

		var allTemplates = CdnEndpoint.Defaults.Select(e => e.UrlTemplate).ToList();
		var ordered = new List<string>();
		if (_selectedCdnIndex >= 0 && _selectedCdnIndex < allTemplates.Count)
			ordered.Add(string.Format(allTemplates[_selectedCdnIndex], appId));
		for (int i = 0; i < allTemplates.Count; i++)
			if (i != _selectedCdnIndex)
				ordered.Add(string.Format(allTemplates[i], appId));

		foreach (var url in ordered)
		{
			try
			{
				var response = await _coverHttp.GetAsync(url, cancellationToken);
				if (!response.IsSuccessStatusCode) continue;

				var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
				if (bytes.Length <= 1000) continue;

				await File.WriteAllBytesAsync(localPath, bytes, cancellationToken);
				return localPath;
			}
			catch (OperationCanceledException) { throw; }
			catch { }
		}
		return null;
	}
}
