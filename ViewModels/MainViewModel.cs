using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteamLuaManager.Models;
using SteamLuaManager.Services;

namespace SteamLuaManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
	private readonly ISteamPathService _steamPathService;
	private readonly ILuaFileManager _luaFileManager;
	private readonly ISteamApiService _steamApiService;
	private readonly INavigationService _navigationService;
	private readonly ISettingsService _settingsService;
	private List<GameInfo> _allGames = new();
	private CancellationTokenSource? _refreshCts;
	private System.Windows.Threading.DispatcherTimer? _progressTimer;

	[ObservableProperty]
	private string _currentView = "Home";

	[ObservableProperty]
	private ObservableCollection<GameInfo> _games = new();

	[ObservableProperty]
	private string _searchText = string.Empty;

	[ObservableProperty]
	private string _statusText = string.Empty;

	[ObservableProperty]
	private string _steamPath = string.Empty;

	[ObservableProperty]
	private string _openSteamToolStatus = string.Empty;

	[ObservableProperty]
	private bool _isAutoRefreshEnabled = true;

	[ObservableProperty]
	private bool _isRefreshing;

	[ObservableProperty]
	private string _selectedSortOption = "名称 A-Z";

	[ObservableProperty]
	private string _selectedViewMode = "卡片";

	[ObservableProperty]
	private bool _isRefreshSlow;

	[ObservableProperty]
	private string _refreshProgressText = string.Empty;

	public MainViewModel(
		ISteamPathService steamPathService,
		ILuaFileManager luaFileManager,
		ISteamApiService steamApiService,
		INavigationService navigationService,
		ISettingsService settingsService)
	{
		_steamPathService = steamPathService;
		_luaFileManager = luaFileManager;
		_steamApiService = steamApiService;
		_navigationService = navigationService;
		_settingsService = settingsService;

		_navigationService.NavigationChanged += (_, view) => CurrentView = view;
		_luaFileManager.FilesChanged += OnFilesChanged;

		var settings = settingsService.Load();
		IsAutoRefreshEnabled = settings.AutoRefreshEnabled;
		SelectedViewMode = settings.SelectedViewMode;
		if (!string.IsNullOrEmpty(settings.SteamPath))
			steamPathService.SetCustomPath(settings.SteamPath);
	}

	[RelayCommand]
	private async Task LoadedAsync()
	{
		var settings = _settingsService.Load();
		var detectedPath = _steamPathService.DetectSteamPath();
		SteamPath = !string.IsNullOrEmpty(settings.SteamPath)
			? settings.SteamPath
			: detectedPath ?? "未检测到Steam";
		OpenSteamToolStatus = _steamPathService.DetectSteamToolType() switch
		{
			SteamToolType.OpenSteamTool => "使用 OpenSteamTool 内核",
			SteamToolType.SteamTools => "检测到不适配的 SteamTools",
			_ => "未安装 OpenSteamTool"
		};
		await RefreshGamesAsync();
		if (IsAutoRefreshEnabled)
			_luaFileManager.StartWatching();
	}

	[RelayCommand]
	private async Task RefreshGamesAsync()
	{
		if (IsRefreshing) return;

		_refreshCts?.Cancel();
		_refreshCts?.Dispose();
		_refreshCts = new CancellationTokenSource();
		var token = _refreshCts.Token;

		IsRefreshSlow = false;

		try
		{
			IsRefreshing = true;

			_ = SlowTimerAsync(token);

			_allGames = await _luaFileManager.ScanLuaFilesAsync();
			_steamApiService.PopulateFromCache(_allGames);
			ApplyFilter();
			UpdateStatus();
			await _steamApiService.RefreshGameInfoAsync(_allGames, token);
			if (!token.IsCancellationRequested)
			{
				ApplyFilter();
				UpdateStatus();
			}
		}
		catch (Exception ex) { StatusText = $"刷新失败: {ex.Message}"; }
		finally
		{
			IsRefreshing = false;
			StopProgressTimer();
			var wasCancelled = token.IsCancellationRequested;
			if (_refreshCts != null)
			{
				_refreshCts.Cancel();
				_refreshCts.Dispose();
				_refreshCts = null;
			}
			if (wasCancelled)
				StatusText = "已取消刷新";
			IsRefreshSlow = false;
		}
	}

	[RelayCommand]
	private void CancelRefresh()
	{
		_refreshCts?.Cancel();
	}

	private async Task SlowTimerAsync(CancellationToken token)
	{
		try
		{
			await Task.Delay(20000, token);
			if (IsRefreshing)
			{
				await Application.Current.Dispatcher.InvokeAsync(() =>
				{
					IsRefreshSlow = true;
					StartProgressTimer();
				});
			}
		}
		catch (OperationCanceledException) { }
	}

	private void StartProgressTimer()
	{
		StopProgressTimer();
		UpdateProgressText();
		_progressTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
		_progressTimer.Tick += (_, _) => UpdateProgressText();
		_progressTimer.Start();
	}

	private void StopProgressTimer()
	{
		if (_progressTimer != null)
		{
			_progressTimer.Stop();
			_progressTimer = null;
		}
	}

	private void UpdateProgressText()
	{
		if (_allGames.Count == 0)
		{
			RefreshProgressText = string.Empty;
			return;
		}
		var done = _allGames.Count(g => !string.IsNullOrEmpty(g.CoverImagePath));
		RefreshProgressText = $"{done} / {_allGames.Count} 个游戏已获取";
	}

	private async Task QuickRefreshAsync()
	{
		if (IsRefreshing) return;
		try
		{
			var newGames = await _luaFileManager.ScanLuaFilesAsync();
			_allGames = newGames;
			_steamApiService.PopulateFromCache(_allGames);
			ApplyFilter();
			UpdateStatus();
			_ = _steamApiService.RefreshGameInfoAsync(_allGames).ContinueWith(_ =>
			{
				Application.Current.Dispatcher.Invoke(() =>
				{
					ApplyFilter();
					UpdateStatus();
				});
			});
		}
		catch (Exception ex) { StatusText = $"刷新失败: {ex.Message}"; }
	}

	partial void OnSearchTextChanged(string value) { ApplyFilter(); UpdateStatus(); }
	partial void OnSelectedSortOptionChanged(string value) { ApplyFilter(); }
	partial void OnSelectedViewModeChanged(string value)
	{
		var settings = _settingsService.Load();
		settings.SelectedViewMode = value;
		_settingsService.Save(settings);
	}

	private void ApplyFilter()
	{
		var query = SearchText?.Trim() ?? string.Empty;
		IEnumerable<GameInfo> filtered = string.IsNullOrWhiteSpace(query)
			? _allGames
			: _allGames.Where(g =>
			{
				var nameMatch = g.GameName.Contains(query, StringComparison.OrdinalIgnoreCase);
				var idMatch = g.AppId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase);
				return nameMatch || idMatch;
			});

		filtered = SelectedSortOption switch
		{
			"名称 Z-A" => filtered.OrderByDescending(g => g.GameName),
			"AppID 升序" => filtered.OrderBy(g => g.AppId),
			"AppID 降序" => filtered.OrderByDescending(g => g.AppId),
			_ => filtered.OrderBy(g => g.GameName)
		};

		Games.Clear();
		foreach (var game in filtered) Games.Add(game);
	}

	private void UpdateStatus() => StatusText = $"共 {Games.Count} 个游戏";

	private async void OnFilesChanged(object? sender, EventArgs e)
	{
		await Application.Current.Dispatcher.InvokeAsync(async () => await QuickRefreshAsync());
	}

	[RelayCommand]
	private async Task AddFilesAsync()
	{
		var dialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Lua文件 (*.lua)|*.lua",
			Multiselect = true,
			Title = "选择Lua文件"
		};

		if (dialog.ShowDialog() == true)
		{
			foreach (var file in dialog.FileNames)
			{
				try { await _luaFileManager.AddLuaFileAsync(file); }
				catch (Exception ex) { StatusText = $"添加失败: {ex.Message}"; }
			}
			await QuickRefreshAsync();
		}
	}

	[RelayCommand]
	private async Task DeleteGameAsync(GameInfo? game)
	{
		if (game == null) return;
		var result = MessageBox.Show(
			$"确定要删除 {game.GameName} ({game.AppId}) 的Lua文件吗？",
			"确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question);

		if (result == MessageBoxResult.Yes)
		{
			try { await _luaFileManager.DeleteLuaFileAsync(game.AppId); await QuickRefreshAsync(); }
			catch (Exception ex) { StatusText = $"删除失败: {ex.Message}"; }
		}
	}

	[RelayCommand]
	private void EditGame(GameInfo? game)
	{
		if (game == null) return;
		var luaFolder = _steamPathService.GetLuaFolder();
		if (string.IsNullOrEmpty(luaFolder)) return;
		var filePath = Path.Combine(luaFolder, $"{game.AppId}.lua");
		if (File.Exists(filePath))
		{
			try { Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true }); }
			catch (Exception ex) { StatusText = $"打开失败: {ex.Message}"; }
		}
	}

	[RelayCommand]
	private void OpenLuaFolder()
	{
		var luaFolder = _steamPathService.GetLuaFolder();
		if (!string.IsNullOrEmpty(luaFolder) && Directory.Exists(luaFolder))
			Process.Start(new ProcessStartInfo { FileName = luaFolder, UseShellExecute = true });
	}

	public async Task HandleDropAsync(string[] files)
	{
		foreach (var file in files)
		{
			if (file.EndsWith(".lua", StringComparison.OrdinalIgnoreCase))
			{
				try { await _luaFileManager.AddLuaFileAsync(file); }
				catch (Exception ex) { StatusText = $"拖拽添加失败: {ex.Message}"; }
			}
		}
		await QuickRefreshAsync();
	}
}
