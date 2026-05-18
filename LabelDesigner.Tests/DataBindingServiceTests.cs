using LabelDesigner.Application.Services;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Tests;

public class DataBindingServiceTests
{
    [Fact]
    public void ResolveTemplate_replaces_placeholders_and_defaults_missing_values()
    {
        var service = new DataBindingService();
        var record = new Dictionary<string, string>
        {
            ["name"] = "Peter"
        };

        var result = service.ResolveTemplate("{{ name }}-{{missing}}", record);

        Assert.Equal("Peter-", result);
    }

    [Fact]
    public void ApplyRecord_preserves_common_fields_and_binds_text_elements()
    {
        var service = new DataBindingService();
        var layerId = Guid.NewGuid();
        var elementId = Guid.NewGuid();
        var doc = new SceneDocument
        {
            Version = "2.0",
            Page = new PageNode
            {
                WidthMm = 210,
                HeightMm = 297,
                Dpi = 300,
                Margins = new Margins(5, 6, 7, 8)
            },
            DataSource = new DataSourceConfig
            {
                Type = "csv",
                Path = "sample.csv",
                MergeMode = nameof(DataMergeMode.MultipleRecordsPerPage)
            }
        };

        var layer = new LayerNode { Id = layerId, Name = "Main", Visible = true, Locked = false };
        layer.ElementIds.Add(elementId);
        doc.Layers.Add(layer);

        var element = new TextElement
        {
            Id = elementId,
            Name = "NameText",
            Bounds = new RectD(10, 20, 100, 25),
            Rotation = 15,
            ScaleX = 1.2,
            ScaleY = 0.8,
            Opacity = 0.7,
            Locked = true,
            Visible = true,
            ParentId = layerId,
            ZIndex = 4,
            Text = "Hello {{ name }}",
            FontSize = 18
        };
        element.Metadata["tag"] = "meta";
        doc.AllElements.Add(element);

        var record = new Dictionary<string, string> { ["name"] = "World" };
        var bound = service.ApplyRecord(doc, record);

        var boundText = Assert.IsType<TextElement>(Assert.Single(bound.AllElements));
        Assert.Equal("Hello World", boundText.Text);
        Assert.Equal(element.Id, boundText.Id);
        Assert.Equal(element.Name, boundText.Name);
        Assert.Equal(element.Bounds.X, boundText.Bounds.X);
        Assert.Equal(element.Rotation, boundText.Rotation);
        Assert.Equal(element.ScaleX, boundText.ScaleX);
        Assert.Equal(element.ScaleY, boundText.ScaleY);
        Assert.Equal(element.Opacity, boundText.Opacity);
        Assert.Equal(element.Locked, boundText.Locked);
        Assert.Equal(element.Visible, boundText.Visible);
        Assert.Equal(element.ParentId, boundText.ParentId);
        Assert.Equal(element.ZIndex, boundText.ZIndex);
        Assert.Equal("meta", boundText.Metadata["tag"]);

        var boundLayer = Assert.Single(bound.Layers);
        Assert.Equal(layerId, boundLayer.Id);
        Assert.Contains(elementId, boundLayer.ElementIds);
        Assert.Equal("csv", bound.DataSource?.Type);
        Assert.Equal("sample.csv", bound.DataSource?.Path);
        Assert.Equal(nameof(DataMergeMode.MultipleRecordsPerPage), bound.DataSource?.MergeMode);
        Assert.Equal(5, bound.Page.Margins.Left);
        Assert.Equal(8, bound.Page.Margins.Bottom);
    }
}
