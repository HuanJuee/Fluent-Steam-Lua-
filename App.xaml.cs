using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SteamLuaManager.Services;
using SteamLuaManager.ViewModels;
using SteamLuaManager.Views;

namespace SteamLuaManager;

public partial class App : Application
{
    public static IServiceProvider? ServiceProvider { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        base.OnStartup(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISteamPathService, SteamPathService>();
        services.AddSingleton<ILuaFileManager, LuaFileManager>();
        services.AddSingleton<ISteamApiService, SteamApiService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        services.AddTransient<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ScriptDownloadViewModel>();
        services.AddTransient<MainWindow>();
    }
}
