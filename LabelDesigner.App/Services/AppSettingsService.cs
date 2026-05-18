using Microsoft.UI.Xaml;

namespace LabelDesigner.App.Services;

public enum MeasurementUnit
{
    Millimeters,
    Centimeters,
    Inches
}

public static class AppSettingsService
{
    public static ElementTheme AppTheme => ElementTheme.Default;

    private static MeasurementUnit _rulerUnit = MeasurementUnit.Millimeters;
    private static bool _showSnapGrid = true;

    public static event Action? SettingsChanged;

    public static MeasurementUnit RulerUnit
    {
        get => _rulerUnit;
        set
        {
            if (_rulerUnit == value) return;
            _rulerUnit = value;
            SettingsChanged?.Invoke();
        }
    }

    public static bool ShowSnapGrid
    {
        get => _showSnapGrid;
        set
        {
            if (_showSnapGrid == value) return;
            _showSnapGrid = value;
            SettingsChanged?.Invoke();
        }
    }
}
