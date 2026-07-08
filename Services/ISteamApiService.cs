using SteamLuaManager.Models;

namespace SteamLuaManager.Services;

public interface ISteamApiService
{
    void PopulateFromCache(List<GameInfo> games);
    Task RefreshGameInfoAsync(List<GameInfo> games, CancellationToken cancellationToken = default);
    int SelectedCdnIndex { get; }
    void UpdateCdnPreference(int selectedIndex);
    Task<List<(string Name, long LatencyMs, bool IsSuccess)>> TestCdnSpeedAsync();
}