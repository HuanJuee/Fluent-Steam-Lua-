namespace SteamLuaManager.Models;

public class CdnEndpoint
{
    public string Name { get; init; }
    public string UrlTemplate { get; init; }

    public CdnEndpoint(string name, string urlTemplate)
    {
        Name = name;
        UrlTemplate = urlTemplate;
    }

    public static List<CdnEndpoint> Defaults { get; } = new()
    {
        new("Cloudflare CDN (优选)", "https://cdn.cloudflare.steamstatic.com/steam/apps/{0}/header.jpg"),
        new("SteamChina 白山云", "https://cdn.steamchina.pinyuncloud.com/steam/apps/{0}/header.jpg"),
        new("Steam国内直连", "https://media.st.dl.pinyuncloud.com/steam/apps/{0}/header.jpg"),
        new("Akamai 主节点", "https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{0}/header.jpg"),
        new("Akamai 备用", "https://cdn.akamai.steamstatic.com/steam/apps/{0}/header.jpg"),
        new("Akamai 大图", "https://cdn.akamai.steamstatic.com/steam/apps/{0}/library_600x900.jpg"),
    };

    public override string ToString() => Name;
}
