using FluentAssertions;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
using LabelDesigner.Infrastructure.Persistence;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LabelDesigner.Tests;

public class SceneDocumentTemplateTests
{
    [Fact]
    public void SceneDocument_json_round_trips_document_defaults_and_page_settings()
    {
        var doc = new SceneDocument
        {
            Version = "2.0",
            Page = new PageNode
            {
                WidthMm = 210,
                HeightMm = 148,
                Dpi = 300,
                Margins = new Margins(4, 5, 6, 7)
            },
            Defaults = new DocumentDefaults
            {
                BarcodeTextPosition = BarcodeTextPosition.Left,
                BarcodeTextFontFamily = "Segoe UI Semibold",
                BarcodeTextFontSize = 13,
                BarcodeTextColor = "#112233",
                TextFontFamily = "Arial",
                TextFontSize = 16,
                TextColor = "#445566",
                TextBold = true,
                TextItalic = true,
                TextUnderline = true,
                TextAlignment = TextAlignmentType.Center,
                TextMultiline = true,
                TextLineSpacing = 1.25
            }
        };

        var layer = new LayerNode { Name = "Layer 1" };
        var barcode = new BarcodeElement
        {
            Bounds = new RectD(10, 20, 120, 50),
            Value = "ABC123",
            Symbology = BarcodeSymbology.QRCode,
            TextPosition = BarcodeTextPosition.Top,
            TextFontFamily = "Segoe UI",
            TextFontSize = 12,
            TextColor = "#000000"
        };
        var text = new TextElement
        {
            Bounds = new RectD(40, 60, 100, 30),
            Text = "Hello",
            FontFamily = "Segoe UI",
            FontSize = 14,
            ForeColor = "#000000",
            TextAlignment = TextAlignmentType.Right
        };

        doc.Layers.Add(layer);
        doc.AllElements.Add(barcode);
        doc.AllElements.Add(text);
        layer.ElementIds.Add(barcode.Id);
        layer.ElementIds.Add(text.Id);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new DesignElementConverter(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        var json = JsonSerializer.Serialize(doc, options);
        var loaded = JsonSerializer.Deserialize<SceneDocument>(json, options)!;

        loaded.Page.WidthMm.Should().Be(210);
        loaded.Page.HeightMm.Should().Be(148);
        loaded.Page.Margins.Should().Be(new Margins(4, 5, 6, 7));
        loaded.Defaults.BarcodeTextPosition.Should().Be(BarcodeTextPosition.Left);
        loaded.Defaults.BarcodeTextFontFamily.Should().Be("Segoe UI Semibold");
        loaded.Defaults.BarcodeTextFontSize.Should().Be(13);
        loaded.Defaults.BarcodeTextColor.Should().Be("#112233");
        loaded.Defaults.TextFontFamily.Should().Be("Arial");
        loaded.Defaults.TextFontSize.Should().Be(16);
        loaded.Defaults.TextColor.Should().Be("#445566");
        loaded.Defaults.TextBold.Should().BeTrue();
        loaded.Defaults.TextItalic.Should().BeTrue();
        loaded.Defaults.TextUnderline.Should().BeTrue();
        loaded.Defaults.TextAlignment.Should().Be(TextAlignmentType.Center);
        loaded.Defaults.TextMultiline.Should().BeTrue();
        loaded.Defaults.TextLineSpacing.Should().Be(1.25);

        var loadedBarcode = loaded.AllElements.OfType<BarcodeElement>().Single();
        loadedBarcode.TextPosition.Should().Be(BarcodeTextPosition.Top);
        loadedBarcode.Symbology.Should().Be(BarcodeSymbology.QRCode);
        loadedBarcode.Value.Should().Be("ABC123");

        var loadedText = loaded.AllElements.OfType<TextElement>().Single();
        loadedText.TextAlignment.Should().Be(TextAlignmentType.Right);
        loadedText.Text.Should().Be("Hello");
    }

    [Fact]
    public async Task Persistence_round_trips_rect_bounds_and_legacy_bounds_shape()
    {
        var service = new JsonLabelPersistenceService();
        var doc = new SceneDocument
        {
            Version = "2.0",
            Page = new PageNode { WidthMm = 100, HeightMm = 50, Dpi = 300 }
        };
        var layer = new LayerNode { Name = "Layer 1" };
        var element = new TextElement
        {
            Bounds = new RectD(12.5, 7.25, 40, 10),
            Text = "Sample"
        };
        doc.Layers.Add(layer);
        doc.AllElements.Add(element);
        layer.ElementIds.Add(element.Id);

        var json = await service.SaveToJsonAsync(doc);
        json.Should().Contain("\"x\": 12.5");
        json.Should().Contain("\"width\": 40");

        var loaded = await service.LoadFromJsonAsync(json);
        var loadedText = Assert.Single(loaded.AllElements.OfType<TextElement>());
        loadedText.Bounds.X.Should().Be(12.5);
        loadedText.Bounds.Y.Should().Be(7.25);
        loadedText.Bounds.Width.Should().Be(40);
        loadedText.Bounds.Height.Should().Be(10);

        const string legacyJson = """
        {
          "version":"1.0",
          "page":{"widthMm":100,"heightMm":50,"dpi":300,"margins":{"left":2,"top":2,"right":2,"bottom":2}},
          "defaults":{"barcodeTextPosition":"bottom","barcodeTextFontFamily":"Segoe UI","barcodeTextFontSize":12,"barcodeTextColor":"#000000","textFontFamily":"Segoe UI","textFontSize":14,"textColor":"#000000","textBold":false,"textItalic":false,"textUnderline":false,"textAlignment":"left","textMultiline":false,"textLineSpacing":0},
          "layers":[{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","name":"Layer 1","visible":true,"locked":false,"elementIds":["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"]}],
          "allElements":[{"type":"TextElement","text":"Legacy","fontSize":14,"fontFamily":"Segoe UI","bold":false,"italic":false,"underline":false,"textAlignment":"left","isMultiline":false,"lineSpacing":0,"foreColor":"#000000","id":"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb","name":"","bounds":{"left":15,"top":20,"right":55,"bottom":30,"centerX":35,"centerY":25},"rotation":0,"scaleX":1,"scaleY":1,"opacity":1,"locked":false,"visible":true,"metadata":{},"parentId":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa","zIndex":0}]
        }
        """;

        var legacyLoaded = await service.LoadFromJsonAsync(legacyJson);
        var legacyText = Assert.Single(legacyLoaded.AllElements.OfType<TextElement>());
        legacyText.Bounds.X.Should().Be(15);
        legacyText.Bounds.Y.Should().Be(20);
        legacyText.Bounds.Width.Should().Be(40);
        legacyText.Bounds.Height.Should().Be(10);
    }

    private sealed class DesignElementConverter : JsonConverter<DesignElement>
    {
        public override DesignElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            var typeName = root.GetProperty("type").GetString()
                ?? throw new JsonException("Missing 'type' discriminator");

            return typeName switch
            {
                nameof(BarcodeElement) => JsonSerializer.Deserialize<BarcodeElement>(root.GetRawText(), options),
                nameof(TextElement) => JsonSerializer.Deserialize<TextElement>(root.GetRawText(), options),
                nameof(ImageElement) => JsonSerializer.Deserialize<ImageElement>(root.GetRawText(), options),
                nameof(ShapeElement) => JsonSerializer.Deserialize<ShapeElement>(root.GetRawText(), options),
                nameof(LineElement) => JsonSerializer.Deserialize<LineElement>(root.GetRawText(), options),
                nameof(ContainerElement) => JsonSerializer.Deserialize<ContainerElement>(root.GetRawText(), options),
                _ => throw new JsonException($"Unknown element type: {typeName}")
            };
        }

        public override void Write(Utf8JsonWriter writer, DesignElement value, JsonSerializerOptions options)
        {
            var runtimeType = value.GetType();
            var clone = new JsonSerializerOptions(options);
            clone.Converters.Remove(this);

            var json = JsonSerializer.Serialize(value, runtimeType, clone);
            using var doc = JsonDocument.Parse(json);
            writer.WriteStartObject();
            writer.WriteString("type", runtimeType.Name);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name != "type")
                    prop.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
    }
}
