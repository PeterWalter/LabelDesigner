using System.Globalization;
using System.Text;

namespace LabelDesigner.Core.Utilities;

public static class DataColumnNameNormalizer
{
    public static List<string> NormalizeUnique(IEnumerable<string?> rawNames, string fallbackPrefix = "Column")
    {
        var normalized = new List<string>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 1;

        foreach (var raw in rawNames)
        {
            var baseName = Canonicalize(raw, index, fallbackPrefix);
            var unique = baseName;
            var suffix = 1;
            while (!used.Add(unique))
            {
                suffix++;
                unique = $"{baseName}_{suffix}";
            }

            normalized.Add(unique);
            index++;
        }

        return normalized;
    }

    public static string Canonicalize(string? raw, int fallbackIndex, string fallbackPrefix = "Column")
    {
        var source = raw ?? string.Empty;
        var normalized = source.Normalize(NormalizationForm.FormKC);
        var sb = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.Format)
                continue;

            if (category == UnicodeCategory.Control && !char.IsWhiteSpace(ch))
                continue;

            sb.Append(ch == '\u00A0' ? ' ' : ch);
        }

        var cleaned = CollapseWhitespace(sb.ToString().Trim().Trim('\uFEFF'));
        if (string.IsNullOrWhiteSpace(cleaned))
            return $"{fallbackPrefix}{fallbackIndex}";

        return cleaned;
    }

    private static string CollapseWhitespace(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var sb = new StringBuilder(value.Length);
        var previousWhitespace = false;

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWhitespace)
                    sb.Append(' ');
                previousWhitespace = true;
            }
            else
            {
                sb.Append(ch);
                previousWhitespace = false;
            }
        }

        return sb.ToString();
    }
}
