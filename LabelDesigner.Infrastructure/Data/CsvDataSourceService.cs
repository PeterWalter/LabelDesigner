using CsvHelper;
using CsvHelper.Configuration;
using LabelDesigner.Core.Interfaces;
using System.Globalization;
using System.Text;

namespace LabelDesigner.Infrastructure.Data;

public class CsvDataSourceService : IDataSourceService
{
    public async Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadAsync(string path)
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
}
