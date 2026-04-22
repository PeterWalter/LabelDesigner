using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LabelDesigner.Core;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using Windows.Foundation;
using LabelDesigner.Infrastructure.Services;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace LabelDesigner.App;

public partial class DesignerViewModel : ObservableObject
{
    private readonly SnapService _snap = new();
    private bool _isDragging = false;
    public RectD PageBounds { get; set; } = new RectD(50, 50, 800, 1100);
    public List<GuideLine> Guides { get; } = new();
    public ObservableCollection<DesignElement> Elements { get; } = new();

    [ObservableProperty]
    private DesignElement? selected;

    private ResizeHandle _activeHandle = ResizeHandle.None;
    private Point _startPoint;
    private RectD _originalBounds;

    private ResizeHandle _hoverHandle = ResizeHandle.None;

    private Point _lastPoint;

    [RelayCommand]
    private void DeleteSelected()
    {
        if (Selected != null)
            Elements.Remove(Selected);
    }

    [RelayCommand]
    private void AddBarcode()
    {
        Elements.Add(new BarcodeElement
        {
            Bounds = new RectD(50, 50, 200, 100)
        });
    }
    public void ToggleOrientation()
    {
        PageBounds = new RectD(
            PageBounds.X,
            PageBounds.Y,
            PageBounds.Height,
            PageBounds.Width);
    }
    private RectD SnapToGrid(RectD rect)
    {
        int grid = 20;

        return new RectD(
            Math.Round(rect.X / grid) * grid,
            Math.Round(rect.Y / grid) * grid,
            rect.Width,
            rect.Height);
    }
    public void PointerPressed(Point p)
    {
        _startPoint = p;
        _isDragging = true;

        var hit = Elements.FirstOrDefault(e =>
        {
            var b = e.Bounds;
            return p.X >= b.X && p.X <= b.X + b.Width &&
                   p.Y >= b.Y && p.Y <= b.Y + b.Height;
        });

        if (hit == null)
        {
            Selected = null;
            _activeHandle = ResizeHandle.None;
            _isDragging = false;
            return;
        }

        Selected = hit;

        _activeHandle = GetHoverHandle(p);
        _originalBounds = Selected.Bounds;
    }

    public void PointerMoved(Point p)
    {
        if (!_isDragging || Selected == null)
            return;

        var dx = p.X - _startPoint.X;
        var dy = p.Y - _startPoint.Y;

        if (_activeHandle == ResizeHandle.None || _activeHandle == ResizeHandle.Move)
        {
            Selected.Bounds = SnapToGrid(_originalBounds.Translate(dx, dy));
        }
        else
        {
            Resize(dx, dy);
        }
    }

    public void PointerReleased()
    {
        _isDragging = false;
        _activeHandle = ResizeHandle.None;
    }

    private ResizeHandle DetectHandle(Point p)
    {
        if (Selected == null) return ResizeHandle.None;

        var b = Selected.Bounds;
        const double size = 8;

        if (Near(p, b.X, b.Y)) return ResizeHandle.TopLeft;
        if (Near(p, b.X + b.Width, b.Y)) return ResizeHandle.TopRight;
        if (Near(p, b.X + b.Width, b.Y + b.Height)) return ResizeHandle.BottomRight;
        if (Near(p, b.X, b.Y + b.Height)) return ResizeHandle.BottomLeft;

        return ResizeHandle.None;
    }

    private bool Near(Point p, double x, double y)
    {
        const double size = 10; // 🔥 increase from 8 → 10 for stability
        return Math.Abs(p.X - x) <= size && Math.Abs(p.Y - y) <= size;
    }

    private void Resize(double dx, double dy)
    {
        var b = _originalBounds;

        double minSize = 20;

        switch (_activeHandle)
        {
            case ResizeHandle.TopLeft:
                b = new RectD(
                    b.X + dx,
                    b.Y + dy,
                    b.Width - dx,
                    b.Height - dy);
                break;

            case ResizeHandle.Top:
                b = new RectD(
                    b.X,
                    b.Y + dy,
                    b.Width,
                    b.Height - dy);
                break;

            case ResizeHandle.TopRight:
                b = new RectD(
                    b.X,
                    b.Y + dy,
                    b.Width + dx,
                    b.Height - dy);
                break;

            case ResizeHandle.Right:
                b = new RectD(
                    b.X,
                    b.Y,
                    b.Width + dx,
                    b.Height);
                break;

            case ResizeHandle.BottomRight:
                b = new RectD(
                    b.X,
                    b.Y,
                    b.Width + dx,
                    b.Height + dy);
                break;

            case ResizeHandle.Bottom:
                b = new RectD(
                    b.X,
                    b.Y,
                    b.Width,
                    b.Height + dy);
                break;

            case ResizeHandle.BottomLeft:
                b = new RectD(
                    b.X + dx,
                    b.Y,
                    b.Width - dx,
                    b.Height + dy);
                break;

            case ResizeHandle.Left:
                b = new RectD(
                    b.X + dx,
                    b.Y,
                    b.Width - dx,
                    b.Height);
                break;
        }

        // ✅ enforce minimum size
        if (b.Width < minSize)
            b.Width = minSize;

        if (b.Height < minSize)
            b.Height = minSize;

        Selected!.Bounds = b;
    }

    public ResizeHandle GetHoverHandle(Point p)
    {
        if (Selected == null) return ResizeHandle.None;

        var b = Selected.Bounds;
        const double size = 8;

        bool Near(double x, double y)
            => Math.Abs(p.X - x) < size && Math.Abs(p.Y - y) < size;

        // corners
        if (Near(b.X, b.Y)) return ResizeHandle.TopLeft;
        if (Near(b.X + b.Width, b.Y)) return ResizeHandle.TopRight;
        if (Near(b.X + b.Width, b.Y + b.Height)) return ResizeHandle.BottomRight;
        if (Near(b.X, b.Y + b.Height)) return ResizeHandle.BottomLeft;

        // edges (improved stability + bounded to element)
        const double edgeTolerance = 10;

        // TOP
        if (Math.Abs(p.Y - b.Y) <= edgeTolerance &&
            p.X >= b.X && p.X <= b.X + b.Width)
            return ResizeHandle.Top;

        // RIGHT
        if (Math.Abs(p.X - (b.X + b.Width)) <= edgeTolerance &&
            p.Y >= b.Y && p.Y <= b.Y + b.Height)
            return ResizeHandle.Right;

        // BOTTOM
        if (Math.Abs(p.Y - (b.Y + b.Height)) <= edgeTolerance &&
            p.X >= b.X && p.X <= b.X + b.Width)
            return ResizeHandle.Bottom;

        // LEFT
        if (Math.Abs(p.X - b.X) <= edgeTolerance &&
            p.Y >= b.Y && p.Y <= b.Y + b.Height)
            return ResizeHandle.Left;

        // inside
        if (p.X >= b.X && p.X <= b.X + b.Width &&
            p.Y >= b.Y && p.Y <= b.Y + b.Height)
            return ResizeHandle.Move;

        return ResizeHandle.None;
    }

    public DesignerViewModel()
    {
        Elements.Add(new BarcodeElement
        {
            Bounds = new RectD(100, 100, 200, 100),
            Value = "123456789"
        });

        Elements.Add(new TextElement
        {
            Bounds = new RectD(200, 300, 200, 50),
            Text = "Hello World"
        });
    }
}