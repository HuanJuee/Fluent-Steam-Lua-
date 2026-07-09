using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SteamLuaManager.ViewModels;

namespace SteamLuaManager.Views;

public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;
    private string _prevCategory = "Basic";

    public SettingsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            _viewModel = vm;
            _prevCategory = vm.SelectedSettingsCategory;
            vm.PropertyChanged += OnViewModelPropertyChanged;
            UpdateTabVisuals(vm.SelectedSettingsCategory);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SettingsViewModel.SelectedSettingsCategory) || _viewModel == null)
            return;

        var newCategory = _viewModel.SelectedSettingsCategory;
        if (newCategory == _prevCategory) return;

        var oldSection = _prevCategory == "Basic" ? BasicSection : AdvancedSection;
        var newSection = newCategory == "Basic" ? BasicSection : AdvancedSection;

        if (oldSection != newSection)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (_, _) =>
            {
                oldSection.Visibility = Visibility.Collapsed;
                newSection.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
                newSection.BeginAnimation(OpacityProperty, fadeIn);
            };
            oldSection.BeginAnimation(OpacityProperty, fadeOut);
        }

        UpdateTabVisuals(newCategory);
        _prevCategory = newCategory;
    }

    private void UpdateTabVisuals(string category)
    {
        var selectedFg = FindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.White;
        var unselectedFg = FindResource("TextFillColorSecondaryBrush") as Brush ?? new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));

        if (category == "Basic")
        {
            BasicUnderline.Visibility = Visibility.Visible;
            BasicTabText.Foreground = selectedFg;
            AdvancedUnderline.Visibility = Visibility.Collapsed;
            AdvancedTabText.Foreground = unselectedFg;
        }
        else
        {
            AdvancedUnderline.Visibility = Visibility.Visible;
            AdvancedTabText.Foreground = selectedFg;
            BasicUnderline.Visibility = Visibility.Collapsed;
            BasicTabText.Foreground = unselectedFg;
        }
    }
}
