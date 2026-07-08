using CommunityToolkit.Mvvm.ComponentModel;

namespace SteamLuaManager.Models;

public partial class GameInfo : ObservableObject
{
    [ObservableProperty]
    private int _appId;

    [ObservableProperty]
    private string _luaFilePath = string.Empty;

    [ObservableProperty]
    private string _gameName = string.Empty;

    [ObservableProperty]
    private string _coverImagePath = string.Empty;

    [ObservableProperty]
    private bool _isLoading;
}
