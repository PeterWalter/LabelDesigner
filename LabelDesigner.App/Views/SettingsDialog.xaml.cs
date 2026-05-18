using LabelDesigner.App.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabelDesigner.App.Views;

public sealed partial class SettingsDialog : ContentDialog
{
    public SettingsDialog()
    {
        InitializeComponent();

        // Load current settings
        RulerUnitCombo.SelectedIndex = (int)AppSettingsService.RulerUnit;
        SnapGridToggle.IsOn = AppSettingsService.ShowSnapGrid;
        ThemeCombo.SelectedIndex = (int)AppSettingsService.AppTheme;

        PrimaryButtonClick += OnSaveClicked;
    }

    private void OnSaveClicked(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        AppSettingsService.RulerUnit = (MeasurementUnit)(RulerUnitCombo.SelectedIndex);
        AppSettingsService.ShowSnapGrid = SnapGridToggle.IsOn;
        AppSettingsService.AppTheme = (ElementTheme)(ThemeCombo.SelectedIndex);
        AppSettingsService.Save();
    }
}
