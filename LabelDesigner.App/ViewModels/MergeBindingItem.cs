using System.ComponentModel;

namespace LabelDesigner.App.ViewModels;

/// <summary>Represents a single canvas element's column binding in the Data Merge pane.</summary>
public sealed class MergeBindingItem : INotifyPropertyChanged
{
    private string? _selectedColumn;
    private bool _suppressCallback;

    public Guid ElementId { get; set; }
    public string DisplayName { get; set; } = "";
    public string ElementType { get; set; } = ""; // "Text" or "Barcode"
    public IReadOnlyList<string> AvailableColumns { get; set; } = Array.Empty<string>();
    public Action<MergeBindingItem>? OnColumnSelected { get; set; }

    public string? SelectedColumn
    {
        get => _selectedColumn;
        set
        {
            if (_selectedColumn == value) return;
            _selectedColumn = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedColumn)));
            if (!_suppressCallback)
                OnColumnSelected?.Invoke(this);
        }
    }

    /// <summary>Sets SelectedColumn without triggering the OnColumnSelected callback.</summary>
    public void SetColumnSilently(string? column)
    {
        _suppressCallback = true;
        SelectedColumn = column;
        _suppressCallback = false;
    }

    /// <summary>Segoe MDL2 glyph code for element type icon.</summary>
    public string TypeGlyph => ElementType == "Barcode" ? "\uE8A1" : "\uE8D2";

    public event PropertyChangedEventHandler? PropertyChanged;
}
