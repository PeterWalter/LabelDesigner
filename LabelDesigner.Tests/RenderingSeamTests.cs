using LabelDesigner.Application.Services;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.Rendering;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Tests;

public class RenderingSeamTests
{
    private static SceneDocument BuildDocument(Action<SceneDocument> configure)
    {
        var doc = new SceneDocument
        {
            Page = new PageNode { WidthMm = 100, HeightMm = 50 }
        };
        configure(doc);
        return doc;
    }

    private static LayerNode AddVisibleLayer(SceneDocument doc, string name = "Layer 1")
    {
        var layer = new LayerNode { Name = name, Visible = true };
        doc.Layers.Add(layer);
        return layer;
    }

    private static void AddElement(SceneDocument doc, LayerNode layer, DesignElement el)
    {
        el.ParentId = layer.Id;
        layer.ElementIds.Add(el.Id);
        doc.AllElements.Add(el);
    }

    [Fact]
    public void Render_TextElement_emits_DrawTextCommand_with_correct_properties()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new TextElement
            {
                Text = "Hello World",
                Bounds = new RectD(10, 20, 80, 15),
                FontFamily = "Arial",
                FontSize = 12,
                Bold = true,
                Italic = false,
                ForeColor = "#FF0000"
            });
        });

        var commands = renderer.Render(doc);

        var textCmd = Assert.Single(commands.OfType<DrawTextCommand>());
        Assert.Equal("Hello World", textCmd.Text);
        Assert.Equal(10, textCmd.X);
        Assert.Equal(20, textCmd.Y);
        Assert.Equal(80, textCmd.Width);
        Assert.Equal(15, textCmd.Height);
        Assert.Equal("Arial", textCmd.FontFamily);
        Assert.Equal(12, textCmd.FontSize);
        Assert.True(textCmd.Bold);
        Assert.False(textCmd.Italic);
        Assert.Equal("#FF0000", textCmd.Color);
    }

    [Fact]
    public void Render_hidden_layer_produces_no_commands()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = new LayerNode { Name = "Hidden", Visible = false };
            d.Layers.Add(layer);
            AddElement(d, layer, new TextElement { Text = "Should not appear" });
        });

        var commands = renderer.Render(doc);

        Assert.Empty(commands);
    }

    [Fact]
    public void Render_hidden_element_within_visible_layer_produces_no_commands()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new TextElement
            {
                Text = "Hidden element",
                Visible = false
            });
        });

        var commands = renderer.Render(doc);

        Assert.Empty(commands);
    }

    [Fact]
    public void Render_ShapeElement_rectangle_emits_DrawRectCommand()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new ShapeElement
            {
                Type = ShapeType.Rectangle,
                Bounds = new RectD(5, 5, 30, 20),
                Fill = "#AABBCC",
                Stroke = "#112233",
                StrokeWidth = 2
            });
        });

        var commands = renderer.Render(doc);

        var rectCmd = Assert.Single(commands.OfType<DrawRectCommand>());
        Assert.Equal(5, rectCmd.X);
        Assert.Equal(5, rectCmd.Y);
        Assert.Equal(30, rectCmd.Width);
        Assert.Equal(20, rectCmd.Height);
        Assert.Equal("#AABBCC", rectCmd.Fill);
        Assert.Equal("#112233", rectCmd.Stroke);
        Assert.Equal(2, rectCmd.StrokeWidth);
    }

    [Fact]
    public void Render_ShapeElement_ellipse_emits_DrawEllipseCommand()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new ShapeElement
            {
                Type = ShapeType.Ellipse,
                Bounds = new RectD(0, 0, 40, 20),
                Fill = "#FF0000",
                Stroke = "#0000FF",
                StrokeWidth = 1
            });
        });

        var commands = renderer.Render(doc);

        var ellipseCmd = Assert.Single(commands.OfType<DrawEllipseCommand>());
        Assert.Equal(20, ellipseCmd.CX);
        Assert.Equal(10, ellipseCmd.CY);
        Assert.Equal(20, ellipseCmd.RadiusX);
        Assert.Equal(10, ellipseCmd.RadiusY);
    }

    [Fact]
    public void Render_LineElement_emits_DrawLineCommand()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new LineElement
            {
                X1 = 5, Y1 = 10, X2 = 50, Y2 = 30,
                Stroke = "#333333",
                StrokeWidth = 2
            });
        });

        var commands = renderer.Render(doc);

        var lineCmd = Assert.Single(commands.OfType<DrawLineCommand>());
        Assert.Equal(5, lineCmd.X1);
        Assert.Equal(10, lineCmd.Y1);
        Assert.Equal(50, lineCmd.X2);
        Assert.Equal(30, lineCmd.Y2);
        Assert.Equal("#333333", lineCmd.Color);
        Assert.Equal(2, lineCmd.StrokeWidth);
    }

    [Fact]
    public void Render_ImageElement_emits_DrawImageCommand()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new ImageElement
            {
                Bounds = new RectD(0, 0, 60, 40),
                SourcePath = "/tmp/test.png"
            });
        });

        var commands = renderer.Render(doc);

        var imgCmd = Assert.Single(commands.OfType<DrawImageCommand>());
        Assert.Equal("/tmp/test.png", imgCmd.SourcePath);
        Assert.Equal(60, imgCmd.Width);
        Assert.Equal(40, imgCmd.Height);
    }

    [Fact]
    public void Render_elements_ordered_by_z_index_back_to_front()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            AddElement(d, layer, new TextElement { Text = "Front", ZIndex = 10 });
            AddElement(d, layer, new TextElement { Text = "Back", ZIndex = 1 });
        });

        var commands = renderer.Render(doc);
        var texts = commands.OfType<DrawTextCommand>().Select(c => c.Text).ToList();

        Assert.Equal("Back", texts[0]);
        Assert.Equal("Front", texts[1]);
    }

    [Fact]
    public void Render_multiple_layers_in_order()
    {
        var renderer = new SceneCommandRenderer();
        var doc = BuildDocument(d =>
        {
            var layer1 = AddVisibleLayer(d, "Bottom");
            var layer2 = AddVisibleLayer(d, "Top");
            AddElement(d, layer1, new TextElement { Text = "Bottom layer" });
            AddElement(d, layer2, new TextElement { Text = "Top layer" });
        });

        var commands = renderer.Render(doc);
        var texts = commands.OfType<DrawTextCommand>().Select(c => c.Text).ToList();

        Assert.Equal("Bottom layer", texts[0]);
        Assert.Equal("Top layer", texts[1]);
    }

    [Fact]
    public void Render_duplicate_element_ids_does_not_throw()
    {
        var renderer = new SceneCommandRenderer();
        var duplicateId = Guid.NewGuid();
        var doc = BuildDocument(d =>
        {
            var layer = AddVisibleLayer(d);
            var first = new TextElement { Id = duplicateId, Text = "First", ZIndex = 0 };
            var second = new TextElement { Id = duplicateId, Text = "Second", ZIndex = 1 };
            layer.ElementIds.Add(duplicateId);
            layer.ElementIds.Add(duplicateId);
            d.AllElements.Add(first);
            d.AllElements.Add(second);
        });

        var commands = renderer.Render(doc);
        var textCommands = commands.OfType<DrawTextCommand>().ToList();

        Assert.NotEmpty(textCommands);
    }
}
