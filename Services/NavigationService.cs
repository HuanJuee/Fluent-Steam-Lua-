namespace SteamLuaManager.Services;

public class NavigationService : INavigationService
{
    private string _currentView = "Home";

    public string CurrentView => _currentView;

    public event EventHandler<string>? NavigationChanged;

    public void NavigateTo(string viewName)
    {
        if (_currentView == viewName) return;
        _currentView = viewName;
        NavigationChanged?.Invoke(this, viewName);
    }
}
