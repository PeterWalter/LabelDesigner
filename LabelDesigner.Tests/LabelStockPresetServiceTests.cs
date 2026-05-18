using LabelDesigner.Application.Services;
using LabelDesigner.Core.Models;

namespace LabelDesigner.Tests;

public class LabelStockPresetServiceTests
{
    [Fact]
    public void GetAll_returns_non_empty_list()
    {
        var service = new LabelStockPresetService();

        var presets = service.GetAll();

        Assert.NotEmpty(presets);
    }

    [Fact]
    public void GetAll_includes_Avery_Dymo_and_Zebra_manufacturers()
    {
        var service = new LabelStockPresetService();

        var manufacturers = service.GetAll()
            .Select(p => p.Manufacturer)
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        Assert.Contains("Avery", manufacturers);
        Assert.Contains("Dymo", manufacturers);
        Assert.Contains("Zebra", manufacturers);
    }

    [Fact]
    public void GetById_returns_correct_Avery5160_preset()
    {
        var service = new LabelStockPresetService();

        var preset = service.GetById("avery-5160");

        Assert.NotNull(preset);
        Assert.Equal("Avery", preset.Manufacturer);
        Assert.Equal(66.68, preset.LabelWidthMm, precision: 2);
        Assert.Equal(25.4, preset.LabelHeightMm, precision: 2);
        Assert.Equal(10, preset.Rows);
        Assert.Equal(3, preset.Columns);
    }

    [Fact]
    public void GetById_returns_null_for_unknown_id()
    {
        var service = new LabelStockPresetService();

        var preset = service.GetById("no-such-preset");

        Assert.Null(preset);
    }

    [Fact]
    public void SaveCustom_adds_preset_to_GetAll()
    {
        var service = new LabelStockPresetService();
        var custom = new LabelStockPreset
        {
            Id = "custom-001",
            DisplayName = "My Custom Label",
            Manufacturer = "Custom",
            LabelWidthMm = 40,
            LabelHeightMm = 20,
            Rows = 1,
            Columns = 1,
            IsBuiltIn = false
        };

        service.SaveCustom(custom);

        var preset = service.GetById("custom-001");
        Assert.NotNull(preset);
        Assert.Equal("My Custom Label", preset.DisplayName);
        Assert.Equal(40, preset.LabelWidthMm);
        Assert.False(preset.IsBuiltIn);
    }

    [Fact]
    public void SaveCustom_replaces_existing_custom_preset_with_same_id()
    {
        var service = new LabelStockPresetService();
        var first = new LabelStockPreset
        {
            Id = "custom-dup",
            DisplayName = "First",
            Manufacturer = "Custom",
            LabelWidthMm = 10,
            LabelHeightMm = 5,
            IsBuiltIn = false
        };
        var second = new LabelStockPreset
        {
            Id = "custom-dup",
            DisplayName = "Updated",
            Manufacturer = "Custom",
            LabelWidthMm = 20,
            LabelHeightMm = 10,
            IsBuiltIn = false
        };

        service.SaveCustom(first);
        service.SaveCustom(second);

        var presets = service.GetAll().Where(p => p.Id == "custom-dup").ToList();
        Assert.Single(presets);
        Assert.Equal("Updated", presets[0].DisplayName);
    }

    [Fact]
    public void RemoveCustom_removes_a_custom_preset()
    {
        var service = new LabelStockPresetService();
        var custom = new LabelStockPreset
        {
            Id = "to-remove",
            DisplayName = "Remove Me",
            Manufacturer = "Custom",
            LabelWidthMm = 30,
            LabelHeightMm = 15,
            IsBuiltIn = false
        };

        service.SaveCustom(custom);
        Assert.NotNull(service.GetById("to-remove"));

        service.RemoveCustom("to-remove");

        Assert.Null(service.GetById("to-remove"));
    }

    [Fact]
    public void SaveCustom_throws_when_preset_is_marked_as_built_in()
    {
        var service = new LabelStockPresetService();
        var invalid = new LabelStockPreset
        {
            Id = "fake-builtin",
            DisplayName = "Fake",
            Manufacturer = "Test",
            LabelWidthMm = 10,
            LabelHeightMm = 5,
            IsBuiltIn = true  // should not be allowed
        };

        Assert.Throws<ArgumentException>(() => service.SaveCustom(invalid));
    }

    [Fact]
    public void GetAll_is_ordered_by_manufacturer_then_display_name()
    {
        var service = new LabelStockPresetService();

        var presets = service.GetAll().ToList();

        for (int i = 1; i < presets.Count; i++)
        {
            var prev = presets[i - 1];
            var curr = presets[i];
            int manufacturerComp = string.Compare(prev.Manufacturer, curr.Manufacturer, StringComparison.Ordinal);
            Assert.True(
                manufacturerComp < 0 ||
                (manufacturerComp == 0 && string.Compare(prev.DisplayName, curr.DisplayName, StringComparison.Ordinal) <= 0),
                $"Ordering violated: '{prev.Manufacturer}/{prev.DisplayName}' should come before '{curr.Manufacturer}/{curr.DisplayName}'");
        }
    }
}
