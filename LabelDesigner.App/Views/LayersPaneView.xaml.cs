using LabelDesigner.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace LabelDesigner.App.Views;

public sealed partial class LayersPaneView : UserControl
{
    public event EventHandler<bool>? CollapseChanged;

    public LayersPaneView()
    {
        InitializeComponent();
    }

    public void SetCollapsed(bool collapsed)
    {
        CollapseToggle.Checked -= OnCollapseToggleChanged;
        CollapseToggle.Unchecked -= OnCollapseToggleChanged;
        CollapseToggle.IsChecked = collapsed;
        CollapseToggle.Checked += OnCollapseToggleChanged;
        CollapseToggle.Unchecked += OnCollapseToggleChanged;

        ApplyCollapsedState(collapsed);
    }

    private void OnLayerItemClick(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ElementItemViewModel evm)
        {
            var vm = DataContext as MainViewModel;
            vm?.Designer.Layers.SelectElement(evm.ElementId);
            vm?.Designer.RequestRedraw?.Invoke();
        }
    }

    private void OnLayerHeaderClick(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is LayerItemViewModel lvm)
        {
            var panel = (DataContext as MainViewModel)?.Designer.Layers;
            if (panel != null)
                panel.SelectedLayer = lvm;
        }
    }

    private void OnLayerExpandClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is LayerItemViewModel lvm)
            lvm.IsExpanded = !lvm.IsExpanded;
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
        FooterArea.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseIcon.Glyph = collapsed ? "\uE76C" : "\uE76B";
    }
}
