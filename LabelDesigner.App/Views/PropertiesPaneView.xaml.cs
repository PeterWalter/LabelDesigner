using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LabelDesigner.App.Views;

public sealed partial class PropertiesPaneView : UserControl
{
    public event EventHandler<bool>? CollapseChanged;

    public PropertiesPaneView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the collapsed state without raising <see cref="CollapseChanged"/>.
    /// </summary>
    public void SetCollapsed(bool collapsed)
    {
        CollapseToggle.Checked -= OnCollapseToggleChanged;
        CollapseToggle.Unchecked -= OnCollapseToggleChanged;
        CollapseToggle.IsChecked = collapsed;
        CollapseToggle.Checked += OnCollapseToggleChanged;
        CollapseToggle.Unchecked += OnCollapseToggleChanged;

        ApplyCollapsedState(collapsed);
    }

    private void OnCollapseToggleChanged(object sender, RoutedEventArgs e)
    {
        var isCollapsed = CollapseToggle.IsChecked == true;
        ApplyCollapsedState(isCollapsed);
        CollapseChanged?.Invoke(this, isCollapsed);
    }

    private void ApplyCollapsedState(bool collapsed)
    {
        ContentArea.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        HeaderText.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        // Properties pane is on the right: › collapses (points right = collapse right), ‹ expands (points left = expand left)
        CollapseIcon.Glyph = collapsed ? "\uE76B" : "\uE76C";
    }
}
