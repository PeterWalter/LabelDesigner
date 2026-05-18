using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.Rendering;

namespace LabelDesigner.Application.Services;

/// <summary>
/// Platform-agnostic scene renderer that converts a <see cref="SceneDocument"/>
/// into a list of <see cref="DrawCommand"/>s in back-to-front paint order.
/// Hidden layers and hidden elements produce no commands.
/// </summary>
public class SceneCommandRenderer : ISceneCommandRenderer
{
    public IReadOnlyList<DrawCommand> Render(SceneDocument document)
    {
        var commands = new List<DrawCommand>();
        var lookup = document.AllElements.ToDictionary(e => e.Id);

        foreach (var layer in document.Layers)
        {
            if (!layer.Visible) continue;

            var elements = layer.ElementIds
                .Select(id => lookup.TryGetValue(id, out var el) ? el : null)
                .Where(el => el != null && el!.Visible)
                .OrderBy(el => el!.ZIndex);

            foreach (var el in elements)
            {
                if (el == null) continue;
                RenderElement(el, lookup, commands);
            }
        }

        return commands.AsReadOnly();
    }

    private static void RenderElement(
        DesignElement el,
        Dictionary<Guid, DesignElement> lookup,
        List<DrawCommand> commands)
    {
        switch (el)
        {
            case TextElement txt:
                commands.Add(new DrawTextCommand(
                    txt.Text,
                    txt.Bounds.X, txt.Bounds.Y, txt.Bounds.Width, txt.Bounds.Height,
                    txt.ForeColor,
                    string.IsNullOrEmpty(txt.FontFamily) ? "Segoe UI" : txt.FontFamily,
                    txt.FontSize,
                    txt.Bold,
                    txt.Italic));
                break;

            case ShapeElement shape:
                RenderShape(shape, commands);
                break;

            case LineElement line:
                commands.Add(new DrawLineCommand(
                    line.X1, line.Y1, line.X2, line.Y2,
                    string.IsNullOrEmpty(line.Stroke) ? "#000000" : line.Stroke,
                    line.StrokeWidth));
                break;

            case ImageElement image:
                commands.Add(new DrawImageCommand(
                    image.Bounds.X, image.Bounds.Y,
                    image.Bounds.Width, image.Bounds.Height,
                    image.SourcePath ?? string.Empty));
                break;

            case SvgElement svg:
                commands.Add(new DrawImageCommand(
                    svg.Bounds.X, svg.Bounds.Y,
                    svg.Bounds.Width, svg.Bounds.Height,
                    svg.SourcePath ?? string.Empty));
                break;

            case BarcodeElement barcode:
                // Represent the barcode placeholder as a rect with text overlay.
                commands.Add(new DrawRectCommand(
                    barcode.Bounds.X, barcode.Bounds.Y,
                    barcode.Bounds.Width, barcode.Bounds.Height,
                    "#FFFFFF", "#000000", 1));
                commands.Add(new DrawTextCommand(
                    barcode.Value,
                    barcode.Bounds.X, barcode.Bounds.Y,
                    barcode.Bounds.Width, barcode.Bounds.Height,
                    barcode.TextColor,
                    string.IsNullOrEmpty(barcode.TextFontFamily) ? "Segoe UI" : barcode.TextFontFamily,
                    barcode.TextFontSize,
                    false, false));
                break;

            case ContainerElement container:
                foreach (var childId in container.ChildIds)
                {
                    if (lookup.TryGetValue(childId, out var child) && child.Visible)
                        RenderElement(child, lookup, commands);
                }
                break;
        }
    }

    private static void RenderShape(ShapeElement shape, List<DrawCommand> commands)
    {
        var b = shape.Bounds;
        switch (shape.Type)
        {
            case ShapeType.Ellipse:
                commands.Add(new DrawEllipseCommand(
                    b.X + b.Width / 2, b.Y + b.Height / 2,
                    b.Width / 2, b.Height / 2,
                    shape.Fill, shape.Stroke, shape.StrokeWidth));
                break;

            case ShapeType.Triangle:
                commands.Add(new DrawPolygonCommand(
                    new[]
                    {
                        (b.X + b.Width / 2, b.Y),
                        (b.X, b.Y + b.Height),
                        (b.X + b.Width, b.Y + b.Height)
                    },
                    shape.Fill, shape.Stroke, shape.StrokeWidth));
                break;

            default:
                commands.Add(new DrawRectCommand(
                    b.X, b.Y, b.Width, b.Height,
                    shape.Fill, shape.Stroke, shape.StrokeWidth));
                break;
        }
    }
}
