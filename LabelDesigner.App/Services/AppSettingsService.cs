using Microsoft.UI.Xaml;
using System.Text.Json;

namespace LabelDesigner.App.Services;

public enum MeasurementUnit
{
    Millimeters,
    Centimeters,
    Inches
}

public static class AppSettingsService
{
    private static ElementTheme _appTheme = ElementTheme.Default;

    public static ElementTheme AppTheme
    {
        get => _appTheme;
        set
        {
            if (_appTheme == value) return;
            _appTheme = value;
            SettingsChanged?.Invoke();
        }
    }

    private static MeasurementUnit _rulerUnit = MeasurementUnit.Millimeters;
    private static bool _showSnapGrid = true;
    private static double _layersPaneWidth = 180;
    private static double _propertiesPaneWidth = 220;
    private static bool _layersPaneCollapsed = false;
    private static bool _propertiesPaneCollapsed = false;

    public static event Action? SettingsChanged;
    public static event Action? LayoutChanged;

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

    public static double LayersPaneWidth
    {
        get => _layersPaneWidth;
        set
        {
            if (_layersPaneWidth == value) return;
            _layersPaneWidth = value;
            LayoutChanged?.Invoke();
        }
    }

    public static double PropertiesPaneWidth
    {
        get => _propertiesPaneWidth;
        set
        {
            if (_propertiesPaneWidth == value) return;
            _propertiesPaneWidth = value;
            LayoutChanged?.Invoke();
        }
    }

    public static bool LayersPaneCollapsed
    {
        get => _layersPaneCollapsed;
        set
        {
            if (_layersPaneCollapsed == value) return;
            _layersPaneCollapsed = value;
            LayoutChanged?.Invoke();
        }
    }

    public static bool PropertiesPaneCollapsed
    {
        get => _propertiesPaneCollapsed;
        set
        {
            if (_propertiesPaneCollapsed == value) return;
            _propertiesPaneCollapsed = value;
            LayoutChanged?.Invoke();
        }
    }

    private static string SettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LabelDesigner", "settings.json");

    public static void Save()
    {
        try
        {
            var data = new SettingsData(
                _rulerUnit.ToString(),
                _showSnapGrid,
                _layersPaneWidth,
                _propertiesPaneWidth,
                _layersPaneCollapsed,
                _propertiesPaneCollapsed,
                _appTheme.ToString());

            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(data));
        }
        catch { /* Ignore save errors */ }
    }

    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath)) return;
            var json = File.ReadAllText(SettingsFilePath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data is null) return;

            _rulerUnit = data.RulerUnit switch
            {
                "Centimeters" => MeasurementUnit.Centimeters,
                "Inches" => MeasurementUnit.Inches,
                _ => MeasurementUnit.Millimeters
            };
            _showSnapGrid = data.ShowSnapGrid;
            _layersPaneWidth = data.LayersPaneWidth > 60 ? data.LayersPaneWidth : 180;
            _propertiesPaneWidth = data.PropertiesPaneWidth > 60 ? data.PropertiesPaneWidth : 220;
            _layersPaneCollapsed = data.LayersPaneCollapsed;
            _propertiesPaneCollapsed = data.PropertiesPaneCollapsed;
            _appTheme = data.AppTheme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
        catch { /* Ignore load errors */ }
    }

    private record SettingsData(
        string RulerUnit,
        bool ShowSnapGrid,
        double LayersPaneWidth,
        double PropertiesPaneWidth,
        bool LayersPaneCollapsed,
        bool PropertiesPaneCollapsed,
        string AppTheme = "Default");
}
