using System.Reflection;
using System.Windows.Controls;

namespace SteamLuaManager.Views;

public partial class AboutView : UserControl
{
    public string VersionText { get; }

    public AboutView()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText = version is not null
            ? $"版本 {version.Major}.{version.Minor}.{version.Build}"
            : "版本 1.0.0";
        InitializeComponent();
        DataContext = this;
    }
}
