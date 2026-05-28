using CsvHelper;
using CsvHelper.Configuration;
using LabelDesigner.Core.Interfaces;
using Syncfusion.XlsIO;
using System.Globalization;
using System.Text.Json;

namespace LabelDesigner.Infrastructure.Data;

public class CsvDataSourceService : IDataSourceService
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadAsync(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".csv" => await LoadCsvAsync(path),
            ".xlsx" or ".xls" => LoadExcel(path),
            ".json" => await LoadJsonAsync(path),
            _ => throw new NotSupportedException($"Unsupported data source format: {extension}")
        };
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadCsvAsync(string path)
    {
        var results = new List<IReadOnlyDictionary<string, string>>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = true,
        };

        // Use BOM-aware reader so a UTF-8 BOM doesn't corrupt the first header name
        using var reader = new StreamReader(path, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, config);

        await csv.ReadAsync();
        csv.ReadHeader();

        // Filter out null, empty, or whitespace-only headers (e.g. from trailing commas)
        var rawHeaders = csv.HeaderRecord ?? Array.Empty<string>();
        var headers = rawHeaders
            .Select(h => h?.Trim())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Cast<string>()
            .ToArray();

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers)
                row[header] = csv.GetField(header) ?? "";
            results.Add(row);
        }

        return results;
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadJsonAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        using var json = await JsonDocument.ParseAsync(stream);
        return json.RootElement.ValueKind switch
        {
            JsonValueKind.Array => ParseJsonArray(json.RootElement),
            JsonValueKind.Object when json.RootElement.TryGetProperty("records", out var recordsElement)
                && recordsElement.ValueKind == JsonValueKind.Array => ParseJsonArray(recordsElement),
            JsonValueKind.Object => new[] { ParseJsonObject(json.RootElement) },
            _ => throw new JsonException("JSON data source must be an array of objects, or an object with a 'records' array.")
        };
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> ParseJsonArray(JsonElement array)
    {
        var records = new List<IReadOnlyDictionary<string, string>>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object)
                records.Add(ParseJsonObject(item));
        }
        return records;
    }

    private static IReadOnlyDictionary<string, string> ParseJsonObject(JsonElement obj)
    {
        var record = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in obj.EnumerateObject())
            record[property.Name] = JsonValueToText(property.Value);
        return record;
    }

    private static string JsonValueToText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Undefined => string.Empty,
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => value.ToString()
        };
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> LoadExcel(string path)
    {
        var records = new List<IReadOnlyDictionary<string, string>>();

        using var excelEngine = new ExcelEngine();
        var app = excelEngine.Excel;
        app.DefaultVersion = ExcelVersion.Xlsx;

        var workbook = app.Workbooks.Open(path);
        try
        {
            if (workbook.Worksheets.Count == 0)
                return records;

            var sheet = workbook.Worksheets[0];
            var usedRange = sheet.UsedRange;
            if (usedRange == null || usedRange.LastRow < 1 || usedRange.LastColumn < 1)
                return records;

            var headers = BuildExcelHeaders(sheet, usedRange.LastColumn);
            if (usedRange.LastRow < 2)
                return records;

            for (var rowIndex = 2; rowIndex <= usedRange.LastRow; rowIndex++)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var hasValue = false;

                for (var columnIndex = 1; columnIndex <= usedRange.LastColumn; columnIndex++)
                {
                    var cellText = sheet[rowIndex, columnIndex].DisplayText ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(cellText))
                        hasValue = true;
                    row[headers[columnIndex - 1]] = cellText;
                }

                if (hasValue)
                    records.Add(row);
            }
        }
        finally
        {
            workbook.Close();
        }

        return records;
    }

    private static List<string> BuildExcelHeaders(IWorksheet sheet, int lastColumn)
    {
        var headers = new List<string>(lastColumn);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var columnIndex = 1; columnIndex <= lastColumn; columnIndex++)
        {
            var rawHeader = sheet[1, columnIndex].DisplayText?.Trim();
            var header = string.IsNullOrWhiteSpace(rawHeader) ? $"Column{columnIndex}" : rawHeader;
            var uniqueHeader = header;
            var suffix = 1;
            while (!used.Add(uniqueHeader))
            {
                suffix++;
                uniqueHeader = $"{header}_{suffix}";
            }
            headers.Add(uniqueHeader);
        }

        return headers;
    }
}
