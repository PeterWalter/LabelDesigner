using FluentAssertions;
using LabelDesigner.Core.Enums;
using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;
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
        loadedBarcode.Value.Should().Be("ABC123");

        var loadedText = loaded.AllElements.OfType<TextElement>().Single();
        loadedText.TextAlignment.Should().Be(TextAlignmentType.Right);
        loadedText.Text.Should().Be("Hello");
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
