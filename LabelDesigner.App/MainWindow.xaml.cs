using LabelDesigner.App.Services;
using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

namespace LabelDesigner.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private bool _isDraggingSplitter;
    private bool _isLeftSplitter;
    private double _startPointerX;
    private bool _suppressToggleSync;

    public MainWindow(MainViewModel vm)
    {
        AppSettingsService.Load();

        InitializeComponent();
        ViewModel = vm;
        RootGrid.DataContext = ViewModel;
        ApplyLocalization();
        ApplyThemeFromSettings();
        AppSettingsService.SettingsChanged += ApplyThemeFromSettings;

        RulerUnitComboBox.SelectedIndex = AppSettingsService.RulerUnit switch
        {
            MeasurementUnit.Millimeters => 0,
            MeasurementUnit.Centimeters => 1,
            MeasurementUnit.Inches => 2,
            _ => 0
        };

        // Restore layout
        LayerColumn.Width = AppSettingsService.LayersPaneCollapsed
            ? new GridLength(32)
            : new GridLength(AppSettingsService.LayersPaneWidth);

        PropertiesColumn.Width = AppSettingsService.PropertiesPaneCollapsed
            ? new GridLength(32)
            : new GridLength(AppSettingsService.PropertiesPaneWidth);

        LayersPane.SetCollapsed(AppSettingsService.LayersPaneCollapsed);
        PropertiesPane.SetCollapsed(AppSettingsService.PropertiesPaneCollapsed);

        _suppressToggleSync = true;
        LayersToggle.IsChecked = !AppSettingsService.LayersPaneCollapsed;
        PropertiesToggle.IsChecked = !AppSettingsService.PropertiesPaneCollapsed;
        _suppressToggleSync = false;

        // Sync ribbon toggles when pane collapse buttons are clicked
        LayersPane.CollapseChanged += (s, isCollapsed) =>
        {
            LayerColumn.Width = isCollapsed
                ? new GridLength(32)
                : new GridLength(AppSettingsService.LayersPaneWidth);
            AppSettingsService.LayersPaneCollapsed = isCollapsed;
            _suppressToggleSync = true;
            LayersToggle.IsChecked = !isCollapsed;
            _suppressToggleSync = false;
            AppSettingsService.Save();
        };

        PropertiesPane.CollapseChanged += (s, isCollapsed) =>
        {
            PropertiesColumn.Width = isCollapsed
                ? new GridLength(32)
                : new GridLength(AppSettingsService.PropertiesPaneWidth);
            AppSettingsService.PropertiesPaneCollapsed = isCollapsed;
            _suppressToggleSync = true;
            PropertiesToggle.IsChecked = !isCollapsed;
            _suppressToggleSync = false;
            AppSettingsService.Save();
        };

        ViewModel.Designer.Layers.Refresh(null);

        HorizontalRuler.GuideCreated += (_, pos) => ViewModel.Designer.AddVerticalGuide(pos);
        LeftRuler.GuideCreated += (_, pos) => ViewModel.Designer.AddHorizontalGuide(pos);

        var appWindow = this.AppWindow;
        if (appWindow != null)
            appWindow.Resize(new Windows.Graphics.SizeInt32(1400, 900));
    }

    private void OnLayersPaneToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleSync) return;
        if (LayersPane is null || LayersToggle is null) return;
        var isCollapsed = LayersToggle.IsChecked != true;
        LayersPane.SetCollapsed(isCollapsed);
        LayerColumn.Width = isCollapsed
            ? new GridLength(32)
            : new GridLength(AppSettingsService.LayersPaneWidth);
        AppSettingsService.LayersPaneCollapsed = isCollapsed;
        AppSettingsService.Save();
    }

    private void OnPropertiesPaneToggled(object sender, RoutedEventArgs e)
    {
        if (_suppressToggleSync) return;
        if (PropertiesPane is null || PropertiesToggle is null) return;
        var isCollapsed = PropertiesToggle.IsChecked != true;
        PropertiesPane.SetCollapsed(isCollapsed);
        PropertiesColumn.Width = isCollapsed
            ? new GridLength(32)
            : new GridLength(AppSettingsService.PropertiesPaneWidth);
        AppSettingsService.PropertiesPaneCollapsed = isCollapsed;
        AppSettingsService.Save();
    }

    private void OnResetLayout(object sender, RoutedEventArgs e)
    {
        AppSettingsService.LayersPaneWidth = 180;
        AppSettingsService.PropertiesPaneWidth = 220;
        AppSettingsService.LayersPaneCollapsed = false;
        AppSettingsService.PropertiesPaneCollapsed = false;

        LayerColumn.Width = new GridLength(180);
        PropertiesColumn.Width = new GridLength(220);

        LayersPane.SetCollapsed(false);
        PropertiesPane.SetCollapsed(false);

        _suppressToggleSync = true;
        LayersToggle.IsChecked = true;
        PropertiesToggle.IsChecked = true;
        _suppressToggleSync = false;

        AppSettingsService.Save();
    }

    private void OnSplitterPressed(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSplitter = true;
        _isLeftSplitter = ReferenceEquals(sender, SplitterLeft);
        _startPointerX = e.GetCurrentPoint(RootGrid).Position.X;
        (sender as UIElement)?.CapturePointer(e.Pointer);
    }

    private void OnSplitterMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter) return;
        var currentX = e.GetCurrentPoint(RootGrid).Position.X;
        var delta = currentX - _startPointerX;

        if (_isLeftSplitter)
        {
            var newWidth = LayerColumn.Width.Value + delta;
            if (newWidth > 60 && newWidth < 500)
            {
                LayerColumn.Width = new GridLength(newWidth);
                _startPointerX = currentX;
            }
        }
        else
        {
            var newWidth = PropertiesColumn.Width.Value - delta;
            if (newWidth > 120 && newWidth < 500)
            {
                PropertiesColumn.Width = new GridLength(newWidth);
                _startPointerX = currentX;
            }
        }
    }

    private void OnSplitterReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingSplitter = false;
        (sender as UIElement)?.ReleasePointerCapture(e.Pointer);

        // Persist new widths (only when pane is expanded)
        if (_isLeftSplitter && !AppSettingsService.LayersPaneCollapsed)
            AppSettingsService.LayersPaneWidth = LayerColumn.Width.Value;
        else if (!_isLeftSplitter && !AppSettingsService.PropertiesPaneCollapsed)
            AppSettingsService.PropertiesPaneWidth = PropertiesColumn.Width.Value;

        AppSettingsService.Save();
    }

    private void OnRulerUnitChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox comboBox || comboBox.SelectedItem is not ComboBoxItem selectedItem)
            return;

        AppSettingsService.RulerUnit = selectedItem.Tag?.ToString() switch
        {
            "Centimeters" => MeasurementUnit.Centimeters,
            "Inches" => MeasurementUnit.Inches,
            _ => MeasurementUnit.Millimeters
        };
    }

    private void OnSnapGridToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb)
            AppSettingsService.ShowSnapGrid = tb.IsChecked == true;
    }

    private async void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new Views.SettingsDialog();
        dialog.XamlRoot = this.Content.XamlRoot;
        await dialog.ShowAsync();
    }

    private void ApplyLocalization()
    {
        Title = LocalizationService.Get("WindowTitle");
        if (RecentFilesLabel != null) RecentFilesLabel.Text = LocalizationService.Get("RecentFilesLabel");
        if (OpenRecentButton != null) OpenRecentButton.Content = LocalizationService.Get("OpenRecentButton");
        if (StockPresetLabel != null) StockPresetLabel.Text = LocalizationService.Get("StockPresetLabel");
    }

    private void ApplyThemeFromSettings()
    {
        if (Content is FrameworkElement root)
            root.RequestedTheme = AppSettingsService.AppTheme;
    }

    private void OnBarcodeFieldSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string fieldName)
        {
            ViewModel.Designer.BindBarcodeToFieldCommand.Execute(fieldName);
            cb.SelectedItem = null; // reset after binding
        }
    }

    private void OnTextFieldSelected(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string fieldName)
        {
            ViewModel.Designer.BindTextToFieldCommand.Execute(fieldName);
            cb.SelectedItem = null;
        }
    }
}
