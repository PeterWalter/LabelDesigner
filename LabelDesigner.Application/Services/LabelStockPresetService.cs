using LabelDesigner.Core.Interfaces;
using LabelDesigner.Core.Models;

namespace LabelDesigner.Application.Services;

/// <summary>
/// In-process implementation of <see cref="ILabelStockPresetService"/>.
/// Built-in presets are defined inline. User-defined presets are stored in a
/// caller-supplied list (or in a subclass that persists to disk).
/// </summary>
public class LabelStockPresetService : ILabelStockPresetService
{
    private static readonly IReadOnlyList<LabelStockPreset> BuiltIn = CreateBuiltInPresets();

    private readonly List<LabelStockPreset> _custom = new();

    public IReadOnlyList<LabelStockPreset> GetAll()
    {
        return BuiltIn
            .Concat(_custom)
            .OrderBy(p => p.Manufacturer)
            .ThenBy(p => p.DisplayName)
            .ToList()
            .AsReadOnly();
    }

    public LabelStockPreset? GetById(string id)
    {
        return BuiltIn.FirstOrDefault(p => p.Id == id)
            ?? _custom.FirstOrDefault(p => p.Id == id);
    }

    public void SaveCustom(LabelStockPreset preset)
    {
        if (preset.IsBuiltIn)
            throw new ArgumentException("Cannot save a built-in preset as a custom preset.", nameof(preset));

        _custom.RemoveAll(p => p.Id == preset.Id);
        _custom.Add(preset);
    }

    public void RemoveCustom(string id)
    {
        _custom.RemoveAll(p => p.Id == id);
    }

    // ── Built-in preset data ─────────────────────────────────────────────────

    private static IReadOnlyList<LabelStockPreset> CreateBuiltInPresets()
    {
        return new List<LabelStockPreset>
        {
            // ── Avery ────────────────────────────────────────────────────────
            new()
            {
                Id = "avery-5160",
                DisplayName = "Avery 5160 — Address Labels (1\" × 2-5/8\")",
                Manufacturer = "Avery",
                LabelWidthMm = 66.68,
                LabelHeightMm = 25.4,
                Rows = 10,
                Columns = 3,
                TopMarginMm = 12.7,
                LeftMarginMm = 4.76,
                HorizontalGapMm = 3.18,
                VerticalGapMm = 0
            },
            new()
            {
                Id = "avery-5163",
                DisplayName = "Avery 5163 — Shipping Labels (2\" × 4\")",
                Manufacturer = "Avery",
                LabelWidthMm = 101.6,
                LabelHeightMm = 50.8,
                Rows = 5,
                Columns = 2,
                TopMarginMm = 12.7,
                LeftMarginMm = 4.76,
                HorizontalGapMm = 3.18,
                VerticalGapMm = 0
            },
            new()
            {
                Id = "avery-5167",
                DisplayName = "Avery 5167 — Return Address Labels (1/2\" × 1-3/4\")",
                Manufacturer = "Avery",
                LabelWidthMm = 44.45,
                LabelHeightMm = 12.7,
                Rows = 20,
                Columns = 4,
                TopMarginMm = 12.7,
                LeftMarginMm = 7.13,
                HorizontalGapMm = 3.18,
                VerticalGapMm = 0
            },
            new()
            {
                Id = "avery-l7160",
                DisplayName = "Avery L7160 — Address Labels 21-up (63.5 × 38.1 mm)",
                Manufacturer = "Avery",
                LabelWidthMm = 63.5,
                LabelHeightMm = 38.1,
                Rows = 7,
                Columns = 3,
                TopMarginMm = 15.15,
                LeftMarginMm = 7.18,
                HorizontalGapMm = 2.54,
                VerticalGapMm = 0
            },
            new()
            {
                Id = "avery-l7163",
                DisplayName = "Avery L7163 — Address Labels 14-up (99.1 × 38.1 mm)",
                Manufacturer = "Avery",
                LabelWidthMm = 99.1,
                LabelHeightMm = 38.1,
                Rows = 7,
                Columns = 2,
                TopMarginMm = 15.15,
                LeftMarginMm = 4.67,
                HorizontalGapMm = 2.54,
                VerticalGapMm = 0
            },

            // ── Dymo ─────────────────────────────────────────────────────────
            new()
            {
                Id = "dymo-30252",
                DisplayName = "Dymo 30252 — Address Label (28 × 89 mm)",
                Manufacturer = "Dymo",
                LabelWidthMm = 89,
                LabelHeightMm = 28,
                Rows = 1,
                Columns = 1,
                TopMarginMm = 0,
                LeftMarginMm = 0,
                HorizontalGapMm = 0,
                VerticalGapMm = 0
            },
            new()
            {
                Id = "dymo-30334",
                DisplayName = "Dymo 30334 — Medium Multipurpose Label (32 × 57 mm)",
                Manufacturer = "Dymo",
                LabelWidthMm = 57,
                LabelHeightMm = 32,
                Rows = 1,
                Columns = 1,
                TopMarginMm = 0,
                LeftMarginMm = 0,
                HorizontalGapMm = 0,
                VerticalGapMm = 0
            },
            new()
            {
                Id = "dymo-30336",
                DisplayName = "Dymo 30336 — Small Multipurpose Label (19 × 51 mm)",
                Manufacturer = "Dymo",
                LabelWidthMm = 51,
                LabelHeightMm = 19,
                Rows = 1,
                Columns = 1,
                TopMarginMm = 0,
                LeftMarginMm = 0,
                HorizontalGapMm = 0,
                VerticalGapMm = 0
            },

            // ── Zebra ─────────────────────────────────────────────────────────
            new()
            {
                Id = "zebra-z-select-2000d-2x1",
                DisplayName = "Zebra Z-Select 2000D — 2\" × 1\" Direct Thermal",
                Manufacturer = "Zebra",
                LabelWidthMm = 50.8,
                LabelHeightMm = 25.4,
                Rows = 1,
                Columns = 1,
                TopMarginMm = 0,
                LeftMarginMm = 0,
                HorizontalGapMm = 0,
                VerticalGapMm = 3.18
            },
            new()
            {
                Id = "zebra-z-select-2000d-4x2",
                DisplayName = "Zebra Z-Select 2000D — 4\" × 2\" Direct Thermal",
                Manufacturer = "Zebra",
                LabelWidthMm = 101.6,
                LabelHeightMm = 50.8,
                Rows = 1,
                Columns = 1,
                TopMarginMm = 0,
                LeftMarginMm = 0,
                HorizontalGapMm = 0,
                VerticalGapMm = 3.18
            },
            new()
            {
                Id = "zebra-z-select-2000d-4x6",
                DisplayName = "Zebra Z-Select 2000D — 4\" × 6\" Shipping Label",
                Manufacturer = "Zebra",
                LabelWidthMm = 101.6,
                LabelHeightMm = 152.4,
                Rows = 1,
                Columns = 1,
                TopMarginMm = 0,
                LeftMarginMm = 0,
                HorizontalGapMm = 0,
                VerticalGapMm = 3.18
            }
        }.AsReadOnly();
    }
}
