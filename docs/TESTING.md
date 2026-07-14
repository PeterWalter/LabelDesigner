<!-- generated-by: gsd-doc-writer -->
# LabelDesigner — Testing

## Test framework and setup

| Tool | Version |
|---|---|
| **xUnit** | 2.9.3 |
| **FluentAssertions** | 8.9.0 |
| **coverlet** | 6.0.4 |
| **Microsoft.NET.Test.Sdk** | 17.14.1 |

The test project (`LabelDesigner.Tests`) targets `net10.0-windows10.0.19041.0` and references `Core`, `Application`, and `Infrastructure`. No special setup is required beyond the standard `dotnet restore`.

---

## Running tests

```bash
# Run all tests
dotnet test LabelDesigner.Tests

# Run without rebuilding
dotnet test LabelDesigner.Tests --no-build

# Run a specific test class
dotnet test LabelDesigner.Tests --filter "FullyQualifiedName~SceneGraphOrderingTests"

# Run with verbose output
dotnet test LabelDesigner.Tests --logger "console;verbosity=normal"

# Collect code coverage (Coverlet)
dotnet test LabelDesigner.Tests --collect:"XPlat Code Coverage"
```

---

## Test coverage areas

| Test file | What it covers |
|---|---|
| `SceneGraphOrderingTests.cs` | Element z-ordering: BringToFront, SendToBack, BringForward, SendBackward |
| `SceneGraphHitTestTests.cs` | Hit testing at world coordinates with and without rotation |
| `ElementInteractionServiceTests.cs` | Pointer-driven interaction: placement, drag, resize handles, rotate |
| `SnapServiceTests.cs` | Grid-snap and guide-snap calculations |
| `JsonLabelPersistenceServiceTests.cs` | Round-trip serialisation of all element types |
| `DataBindingServiceTests.cs` | CSV column binding to element fields |
| `CsvDataSourceServiceTests.cs` | CSV file parsing, column discovery, row iteration |
| `DataColumnNameNormalizerTests.cs` | Header normalisation rules |
| `DataMergeGridModelBuilderTests.cs` | Multi-record merge grid construction |
| `MergeRecordSelectorTests.cs` | Record selection for merge-print mode |
| `LabelStockPresetServiceTests.cs` | Preset lookup and sheet-layout population |
| `RenderingSeamTests.cs` | Draw-command pipeline produces expected commands for each element type |
| `PrintAndPdfRegressionTests.cs` | Scale-factor regression for print and PDF export |
| `WorkbookSheetDataCacheTests.cs` | Workbook/CSV sheet data caching |

---

## Writing new tests

### File naming

Place test files in `LabelDesigner.Tests/`. Name files `<Subject>Tests.cs` where `<Subject>` matches the class or service under test.

### Conventions

```csharp
public class MyServiceTests
{
    [Fact]
    public void MethodName_WhenCondition_ExpectedBehaviour()
    {
        // Arrange
        var service = new MyService();

        // Act
        var result = service.DoSomething();

        // Assert
        result.Should().Be(expected);
    }
}
```

- Use **FluentAssertions** (`result.Should().Be(...)`, `.BeEquivalentTo(...)`, `.Throw<>()`, etc.) — not `Assert.Equal`.
- Use `[Fact]` for single-case tests, `[Theory]` with `[InlineData]` for parameterised cases.
- Tests are pure unit tests — no WinUI or Win2D context required. Infrastructure services that need Win2D are tested via the draw-command seam (see `RenderingSeamTests.cs`).

---

## Coverage thresholds

No minimum coverage threshold is configured in the project. Coverage reports can be generated with `coverlet` (included as a package):

```bash
dotnet test LabelDesigner.Tests \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

Results are written to `./coverage/` as Cobertura XML.

---

## CI integration

<!-- VERIFY: CI workflow name and triggers -->
No GitHub Actions workflow file is currently present in `.github/workflows/`. Tests are run locally or via Visual Studio's Test Explorer.
