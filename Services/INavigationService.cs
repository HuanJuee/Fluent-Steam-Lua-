namespace SteamLuaManager.Services;

public interface INavigationService
{
    string CurrentView { get; }
    event EventHandler<string>? NavigationChanged;
    void NavigateTo(string viewName);
}
