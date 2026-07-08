using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using SteamLuaManager.Services;
using SteamLuaManager.ViewModels;

namespace SteamLuaManager.Views;

public partial class MainWindow : Window
{
    private readonly string[] _navOrder = ["Home", "ScriptDownload", "Settings", "About"];
    private string _prevTag = "Home";

    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly ScriptDownloadViewModel _scriptDownloadViewModel;
    private readonly HomeView _homeView;
    private readonly SettingsView _settingsView;
    private readonly ScriptDownloadView _scriptDownloadView;
    private readonly AboutView _aboutView;

    public MainWindow(MainViewModel viewModel, SettingsViewModel settingsViewModel, ScriptDownloadViewModel scriptDownloadViewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsViewModel = settingsViewModel;
        _scriptDownloadViewModel = scriptDownloadViewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        var iconUri = new Uri("pack://application:,,,/Assets/app.ico");
        var decoder = BitmapDecoder.Create(iconUri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        var bestFrame = decoder.Frames.OrderByDescending(f => f.PixelWidth * f.PixelHeight).First();
        Icon = bestFrame;

        _homeView = new HomeView { DataContext = _viewModel };
        _settingsView = new SettingsView { DataContext = settingsViewModel };
        _scriptDownloadView = new ScriptDownloadView { DataContext = scriptDownloadViewModel };
        _aboutView = new AboutView();
        ContentTransition.Content = _homeView;

        settingsViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedBackdrop) && s is SettingsViewModel svm)
                UpdateBackdrop(svm.SelectedBackdrop);
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel.LoadedCommand.CanExecute(null))
            await _viewModel.LoadedCommand.ExecuteAsync(null);

        Title = $"Fluent Steam Lua 管理工具 - {_viewModel.OpenSteamToolStatus}";

        BackdropHelper.ApplyDarkMode(this);

        var settings = _settingsService.Load();
        if (settings.SelectedBackdrop != "Acrylic10")
            UpdateBackdrop(settings.SelectedBackdrop);

        switch (_viewModel.OpenSteamToolStatus)
        {
            case "未安装 OpenSteamTool":
                System.Windows.MessageBox.Show(
                    "未检测到 OpenSteamTool，本软件目前仅适配 OpenSteamTool。\n\n" +
                    "请确保已在 Steam 目录中正确安装 OpenSteamTool 后再使用。",
                    "未安装 OpenSteamTool",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                break;

            case "检测到不适配的 SteamTools":
                System.Windows.MessageBox.Show(
                    "检测到 SteamTools（闭源），该内核与本软件不适配。\n\n" +
                    "本软件目前仅适配 OpenSteamTool（开源内核）。\n" +
                    "请卸载 SteamTools 后安装 OpenSteamTool 再使用。",
                    "不适配的 SteamTools",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                break;
        }

        HomeItem.IsSelected = true;
    }

    private void UpdateBackdrop(string backdropTypeName)
    {
        if (!Enum.TryParse<BackdropType>(backdropTypeName, true, out var backdropType))
            return;

        WindowHelper.SetSystemBackdropType(this, backdropType);

        if (backdropType == BackdropType.None)
        {
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E));
        }
        else
        {
            BackdropHelper.ApplyDarkMode(this);
            Background = null;
        }
    }

    private void NavView_SelectionChanged(object sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem item || item.Tag is not string tag)
            return;

        if (tag != "ScriptDownload")
        {
            _scriptDownloadViewModel.LogLines.Clear();
            _scriptDownloadViewModel.SearchResults.Clear();
            _scriptDownloadViewModel.StatusMessage = "";
        }
        if (tag != "Settings")
        {
            _settingsViewModel.SpeedTestResults.Clear();
            _settingsViewModel.StatusMessage = "";
        }

        _viewModel.CurrentView = tag;

        var prevIndex = Array.IndexOf(_navOrder, _prevTag);
        var newIndex = Array.IndexOf(_navOrder, tag);
        if (prevIndex >= 0 && newIndex >= 0)
        {
            if (newIndex > prevIndex)
            {
                ContentTransition.Transition = TransitionType.Down;
            }
            else if (newIndex < prevIndex)
            {
                ContentTransition.Transition = TransitionType.Up;
            }
        }
        _prevTag = tag;

        SwitchView(tag);
    }

    private void SwitchView(string tag)
    {
        UserControl? newView = tag switch
        {
            "Home" => _homeView,
            "Settings" => _settingsView,
            "ScriptDownload" => _scriptDownloadView,
            "About" => _aboutView,
            _ => null
        };

        if (newView is null || newView == ContentTransition.Content) return;
        ContentTransition.Content = newView;
    }

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            await _viewModel.HandleDropAsync(files);
        }
    }
}
