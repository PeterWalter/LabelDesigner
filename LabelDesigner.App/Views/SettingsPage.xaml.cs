using LabelDesigner.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabelDesigner.App.Views;

public sealed partial class SettingsPage : UserControl
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RulerUnitCombo.SelectedIndex = (int)AppSettingsService.RulerUnit;
        SnapGridToggle.IsOn = AppSettingsService.ShowSnapGrid;
        ThemeCombo.SelectedIndex = (int)AppSettingsService.AppTheme;
    }

    private void OnRulerUnitChanged(object sender, SelectionChangedEventArgs e)
    {
        AppSettingsService.RulerUnit = (MeasurementUnit)RulerUnitCombo.SelectedIndex;
        AppSettingsService.Save();
    }

    private void OnSnapGridToggled(object sender, RoutedEventArgs e)
    {
        AppSettingsService.ShowSnapGrid = SnapGridToggle.IsOn;
        AppSettingsService.Save();
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        AppSettingsService.AppTheme = (ElementTheme)ThemeCombo.SelectedIndex;
        AppSettingsService.Save();
    }
}
