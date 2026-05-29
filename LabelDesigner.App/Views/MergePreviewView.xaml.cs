using LabelDesigner.App.ViewModels;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace LabelDesigner.App.Views;

public sealed partial class MergePreviewView : UserControl
{
    private DesignerViewModel? _wiredVm;
    private PropertyChangedEventHandler? _propertyChangedHandler;
    private readonly CanvasViewport _viewport = new();
    private bool _firstDraw = true;

    private DesignerViewModel? VM => DataContext switch
    {
        MainViewModel main => main.Designer,
        DesignerViewModel designer => designer,
        _ => null
    };

    public MergePreviewView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        WireViewModel();
        Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WireViewModel(null);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        WireViewModel();
        Invalidate();
    }

    private void WireViewModel(DesignerViewModel? next = null)
    {
        if (_wiredVm != null && _propertyChangedHandler != null)
            _wiredVm.PropertyChanged -= _propertyChangedHandler;

        _wiredVm = next ?? VM;

        if (_wiredVm != null)
        {
            _propertyChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(DesignerViewModel.MergePreviewDocument) ||
                    args.PropertyName == nameof(DesignerViewModel.PreviewRecordText))
                {
                    if (Canvas.ActualWidth > 0 && Canvas.ActualHeight > 0)
                        FitToCanvas(_wiredVm.MergePreviewDocument, Canvas);
                    Invalidate();
                }
            };

            _wiredVm.PropertyChanged += _propertyChangedHandler;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_wiredVm == null || Canvas.ActualWidth <= 0 || Canvas.ActualHeight <= 0)
            return;

        FitToCanvas(_wiredVm.MergePreviewDocument, Canvas);
    }

    private void Invalidate() => Canvas?.Invalidate();

    private void OnDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var vm = VM;
        if (vm == null)
            return;

        var document = vm.MergePreviewDocument;

        if (_firstDraw || sender.ActualWidth <= 0 || sender.ActualHeight <= 0)
        {
            _firstDraw = false;
            if (document != null)
                FitToCanvas(document, sender);
        }

        if (document == null) return;

        var rect = new RectD(_viewport.OffsetX, _viewport.OffsetY, sender.ActualWidth, sender.ActualHeight);
        vm.RenderService.RenderScene(
            args.DrawingSession,
            document,
            Array.Empty<Guid>(),
            Array.Empty<Guid>(),
            (float)_viewport.Zoom,
            rect,
            vm.PixelsPerMm,
            false);
    }

    private void FitToCanvas(SceneDocument document, CanvasControl sender)
    {
        var vm = VM;
        if (vm == null || document == null) return;

        double pageW = document.Page.WidthMm * vm.PixelsPerMm;
        double pageH = document.Page.HeightMm * vm.PixelsPerMm;
        if (pageW <= 0 || pageH <= 0 || sender.ActualWidth <= 0 || sender.ActualHeight <= 0)
            return;

        _viewport.ZoomToFit(sender.ActualWidth, sender.ActualHeight, pageW, pageH);
        sender.Invalidate();
    }
}
