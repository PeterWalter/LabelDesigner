using System.Text.Json;
using System.Text.Json.Serialization;
using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;

namespace LabelDesigner.Infrastructure.Persistence;

public class JsonLabelPersistenceService : ILabelPersistenceService
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new DesignElementConverter(), new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async Task<SceneDocument> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return await LoadFromJsonAsync(json);
    }

    public async Task SaveAsync(SceneDocument document, string filePath)
    {
        var json = await SaveToJsonAsync(document);
        await File.WriteAllTextAsync(filePath, json);
    }

    public Task<SceneDocument> LoadFromJsonAsync(string json)
    {
        var doc = JsonSerializer.Deserialize<SceneDocument>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize SceneDocument");
        return Task.FromResult(doc);
    }

    public Task<string> SaveToJsonAsync(SceneDocument document)
    {
        var json = JsonSerializer.Serialize(document, Options);
        return Task.FromResult(json);
    }
}

internal class DesignElementConverter : JsonConverter<DesignElement>
{
    public override DesignElement? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        var typeName = root.GetProperty("type").GetString()
            ?? throw new JsonException("Missing 'type' discriminator");

        DesignElement? element = typeName switch
        {
            "BarcodeElement" => JsonSerializer.Deserialize<BarcodeElement>(root.GetRawText(), options),
            "TextElement" => JsonSerializer.Deserialize<TextElement>(root.GetRawText(), options),
            "ImageElement" => JsonSerializer.Deserialize<ImageElement>(root.GetRawText(), options),
            "ShapeElement" => JsonSerializer.Deserialize<ShapeElement>(root.GetRawText(), options),
            "LineElement" => JsonSerializer.Deserialize<LineElement>(root.GetRawText(), options),
            "ContainerElement" => JsonSerializer.Deserialize<ContainerElement>(root.GetRawText(), options),
            _ => throw new JsonException($"Unknown element type: {typeName}")
        };

        return element as DesignElement;
    }

    public override void Write(Utf8JsonWriter writer, DesignElement value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        var typeName = type.Name;

        var clone = new JsonSerializerOptions(options);
        clone.Converters.Remove(this); // prevent recursion

        var json = JsonSerializer.Serialize(value, type, clone);
        using var doc = JsonDocument.Parse(json);
        writer.WriteStartObject();
        writer.WriteString("type", typeName);
        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            if (prop.Name != "type")
                prop.WriteTo(writer);
        }
        writer.WriteEndObject();
    }
}
