using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using iNKORE.UI.WPF.Modern.Controls;
using SteamLuaManager.Models;
using SteamLuaManager.ViewModels;

namespace SteamLuaManager.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void SearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.SearchText = e.QueryText ?? string.Empty;
        }
    }

    private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm && e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
        {
            vm.SearchText = sender.Text ?? string.Empty;
        }
    }

    private void ViewModeContainer_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.IsVisibleChanged += (_, args) =>
            {
                if (args.NewValue is true)
                {
                    fe.Opacity = 0;
                    var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                    fe.BeginAnimation(OpacityProperty, animation);
                }
            };
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is GameInfo game && DataContext is MainViewModel vm)
        {
            var menu = new ContextMenu();

            var editItem = new MenuItem { Header = "编辑 Lua" };
            editItem.Click += (_, _) => vm.EditGameCommand.Execute(game);

            var deleteItem = new MenuItem { Header = "删除 Lua" };
            deleteItem.Click += async (_, _) => await vm.DeleteGameCommand.ExecuteAsync(game);

            menu.Items.Add(editItem);
            menu.Items.Add(deleteItem);

            menu.Items.Add(new Separator());

            var pinSubMenu = new MenuItem { Header = "版本固定" };

            if (game.IsManifestPinned)
            {
                var unpinItem = new MenuItem { Header = "取消版本固定" };
                unpinItem.Click += async (_, _) => await vm.UnpinGameCommand.ExecuteAsync(game);
                pinSubMenu.Items.Add(unpinItem);
            }
            else
            {
                var latestItem = new MenuItem { Header = "固定到游戏最新版本" };
                latestItem.Click += async (_, _) => await vm.PinToLatestCommand.ExecuteAsync(game);

                var currentItem = new MenuItem { Header = "固定到当前已安装版本" };
                currentItem.Click += async (_, _) => await vm.PinToCurrentCommand.ExecuteAsync(game);

                pinSubMenu.Items.Add(latestItem);
                pinSubMenu.Items.Add(currentItem);
            }

            menu.Items.Add(pinSubMenu);

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
