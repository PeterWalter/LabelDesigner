namespace LabelDesigner.Application.Data;

public static class MergeRecordSelector
{
    public static IReadOnlyList<IReadOnlyDictionary<string, string>> SelectActiveRecords(
        IReadOnlyCollection<int> selectedRecordIndices,
        IReadOnlyList<IReadOnlyDictionary<string, string>> allRecords)
    {
        ArgumentNullException.ThrowIfNull(selectedRecordIndices);
        ArgumentNullException.ThrowIfNull(allRecords);

        if (selectedRecordIndices.Count == 0)
            return allRecords;

        var selectedSet = new HashSet<int>(selectedRecordIndices);
        return allRecords
            .Where((_, index) => selectedSet.Contains(index))
            .ToList();
    }
}
