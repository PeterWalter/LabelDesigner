using LabelDesigner.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabelDesigner.App.Views;

public sealed partial class SettingsPage : UserControl
{
    private bool _isInitializing = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        RulerUnitCombo.SelectedIndex = (int)AppSettingsService.RulerUnit;
        SnapGridToggle.IsOn = AppSettingsService.ShowSnapGrid;
        ThemeCombo.SelectedIndex = (int)AppSettingsService.AppTheme;
        _isInitializing = false;
    }

    private void OnRulerUnitChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppSettingsService.RulerUnit = (MeasurementUnit)RulerUnitCombo.SelectedIndex;
        AppSettingsService.Save();
    }

    private void OnSnapGridToggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppSettingsService.ShowSnapGrid = SnapGridToggle.IsOn;
        AppSettingsService.Save();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitializing)
            return;

        AppSettingsService.AppTheme = (ElementTheme)ThemeCombo.SelectedIndex;
        AppSettingsService.Save();
    }
}
