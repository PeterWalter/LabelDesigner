using LabelDesigner.Core.Models;

namespace LabelDesigner.Core.Interfaces;

/// <summary>
/// Manages the catalogue of label stock presets (both built-in and
/// user-defined). Implementations may persist custom presets to local storage.
/// </summary>
public interface ILabelStockPresetService
{
    /// <summary>Returns all available presets (built-in and custom), ordered by manufacturer then display name.</summary>
    IReadOnlyList<LabelStockPreset> GetAll();

    /// <summary>Returns the preset with the given <paramref name="id"/>, or <c>null</c> if not found.</summary>
    LabelStockPreset? GetById(string id);

    /// <summary>
    /// Persists a user-defined preset. If a custom preset with the same
    /// <see cref="LabelStockPreset.Id"/> already exists it is replaced.
    /// </summary>
    void SaveCustom(LabelStockPreset preset);

    /// <summary>Removes a user-defined preset. Built-in presets cannot be removed.</summary>
    void RemoveCustom(string id);
}
