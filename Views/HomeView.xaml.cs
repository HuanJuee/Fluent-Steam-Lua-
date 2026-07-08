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

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }
}
