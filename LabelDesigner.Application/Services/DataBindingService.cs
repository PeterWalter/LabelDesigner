using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;

namespace LabelDesigner.Application.Services;

public class DataBindingService : IDataBindingService
{
    public SceneDocument ApplyRecord(SceneDocument document, IReadOnlyDictionary<string, string> record)
    {
        var clone = new SceneDocument
        {
            Version = document.Version,
            Page = new PageNode
            {
                WidthMm = document.Page.WidthMm,
                HeightMm = document.Page.HeightMm,
                Dpi = document.Page.Dpi,
                Margins = new Margins(
                    document.Page.Margins.Left,
                    document.Page.Margins.Top,
                    document.Page.Margins.Right,
                    document.Page.Margins.Bottom)
            },
            DataSource = document.DataSource == null
                ? null
                : new DataSourceConfig
                {
                    Type = document.DataSource.Type,
                    Path = document.DataSource.Path,
                    MergeMode = document.DataSource.MergeMode
                }
        };

        foreach (var layer in document.Layers)
        {
            var clonedLayer = new LayerNode
            {
                Id = layer.Id,
                Name = layer.Name,
                Visible = layer.Visible,
                Locked = layer.Locked
            };
            foreach (var elementId in layer.ElementIds)
            {
                clonedLayer.ElementIds.Add(elementId);
            }

            clone.Layers.Add(clonedLayer);
        }

        foreach (var element in document.AllElements)
        {
            clone.AllElements.Add(BindElement(element, record));
        }

        return clone;
    }

    public string ResolveTemplate(string template, IReadOnlyDictionary<string, string> record)
    {
        var resolved = template;
        int start;

        while ((start = resolved.IndexOf("{{", StringComparison.Ordinal)) >= 0)
        {
            var end = resolved.IndexOf("}}", start, StringComparison.Ordinal);
            if (end < 0)
            {
                break;
            }

            var field = resolved.Substring(start + 2, end - start - 2).Trim();
            var value = record.TryGetValue(field, out var replacement) ? replacement : "";
            resolved = resolved.Substring(0, start) + value + resolved.Substring(end + 2);
        }

        return resolved;
    }

    private DesignElement BindElement(DesignElement source, IReadOnlyDictionary<string, string> record)
    {
        return source switch
        {
            BarcodeElement barcode => CloneElement(
                barcode,
                new BarcodeElement
                {
                    Id = barcode.Id,
                    Value = ResolveTemplate(barcode.Value, record),
                    TextPosition = barcode.TextPosition
                }),
            TextElement text => CloneElement(
                text,
                new TextElement
                {
                    Id = text.Id,
                    Text = ResolveTemplate(text.Text, record),
                    FontSize = text.FontSize
                }),
            ShapeElement shape => CloneElement(
                shape,
                new ShapeElement
                {
                    Id = shape.Id,
                    Type = shape.Type,
                    PathData = shape.PathData,
                    Fill = shape.Fill,
                    Stroke = shape.Stroke,
                    StrokeWidth = shape.StrokeWidth
                }),
            LineElement line => CloneElement(
                line,
                new LineElement
                {
                    Id = line.Id,
                    X1 = line.X1,
                    Y1 = line.Y1,
                    X2 = line.X2,
                    Y2 = line.Y2,
                    Stroke = line.Stroke,
                    StrokeWidth = line.StrokeWidth
                }),
            ImageElement image => CloneElement(
                image,
                new ImageElement
                {
                    Id = image.Id,
                    SourcePath = image.SourcePath,
                    Stretch = image.Stretch
                }),
            ContainerElement container => CloneElement(
                container,
                CloneContainer(container)),
            _ => throw new NotSupportedException($"Unsupported element type for data binding: {source.GetType().FullName}")
        };
    }

    private static ContainerElement CloneContainer(ContainerElement source)
    {
        var container = new ContainerElement { Id = source.Id };
        foreach (var childId in source.ChildIds)
        {
            container.ChildIds.Add(childId);
        }

        return container;
    }

    private static T CloneElement<T>(DesignElement source, T destination) where T : DesignElement
    {
        destination.Name = source.Name;
        destination.Bounds = source.Bounds;
        destination.Rotation = source.Rotation;
        destination.ScaleX = source.ScaleX;
        destination.ScaleY = source.ScaleY;
        destination.Opacity = source.Opacity;
        destination.Locked = source.Locked;
        destination.Visible = source.Visible;
        destination.ParentId = source.ParentId;
        destination.ZIndex = source.ZIndex;

        foreach (var (key, value) in source.Metadata)
        {
            destination.Metadata[key] = value;
        }

        return destination;
    }
}
