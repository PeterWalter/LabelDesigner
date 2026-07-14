<!-- generated-by: gsd-doc-writer -->
# LabelDesigner — Development Guide

## Local setup

```bash
git clone https://github.com/PeterWalter/LabelDesigner.git
cd LabelDesigner
dotnet restore LabelDesigner.slnx
```

Open `LabelDesigner.slnx` in **Visual Studio 2026** with the **Windows App SDK** workload installed. See [GETTING-STARTED.md](GETTING-STARTED.md) for prerequisites.

---

## Build commands

| Command | Description |
|---|---|
| `dotnet build LabelDesigner.slnx` | Build all projects (Debug, x64 by default) |
| `dotnet build LabelDesigner.slnx -c Release` | Release build |
| `dotnet build LabelDesigner.slnx -p:Platform=x86` | Build for x86 |
| `dotnet build LabelDesigner.slnx -p:Platform=ARM64` | Build for ARM64 |
| `dotnet test LabelDesigner.Tests` | Run all unit tests |
| `dotnet test LabelDesigner.Tests --no-build` | Run tests without rebuilding |

The app is packaged as MSIX. `dotnet run` is not supported for WinUI 3 projects — use Visual Studio **F5** or the MSIX package produced by `dotnet build`.

---

## Project layout

```
LabelDesigner.Core          Domain models and service interfaces — no UI, no platform code
LabelDesigner.Application   Service implementations (scene graph, undo, snap, data binding)
LabelDesigner.Infrastructure Platform code (Win2D rendering, SkiaSharp PDF, ZXing barcodes)
LabelDesigner.App           WinUI 3 shell: views, view-models, ribbon, settings
LabelDesigner.Tests         xUnit tests targeting Core, Application, and Infrastructure
```

Dependency rule: `App` → `Application` → `Core` ← `Infrastructure`. The `App` and `Infrastructure` layers may not be imported by `Core` or `Application`.

---

## Code style

This project uses C# 12+ with nullable reference types enabled across all projects. There is no `.editorconfig` or Prettier/ESLint equivalent — follow the conventions already in the codebase:

- **Naming**: PascalCase for types and public members, `_camelCase` for private fields.
- **MVVM**: Observable properties in view-models use `[ObservableProperty]` and `[RelayCommand]` from `CommunityToolkit.Mvvm`.
- **Async in ribbon handlers**: Use `_ = MethodAsync()` wrappers because Syncfusion ribbon buttons cannot bind `AsyncRelayCommand` (see ADR-0003). Methods that open UI dialogs before their first `await` must begin with `await Task.Yield()`.
- **Undo stack**: Every mutation to `SceneDocument` must be wrapped in an `IUndoableCommand` and executed via `IUndoRedoService.Execute(...)`.
- **Coordinates**: Element bounds are stored in screen pixels at actual device DPI. Use `DpiService.PixelsPerMm` for all mm↔px conversions. Do not hardcode 96 DPI.

---

## Adding a new element type

1. Create `LabelDesigner.Core/Models/MyElement.cs` extending `DesignElement`.
2. Add rendering logic to `LabelDesigner.Infrastructure/Rendering/ElementRenderer.cs`.
3. Register it in the JSON persistence polymorphism map in `JsonLabelPersistenceService`.
4. Add placement mode to `PlacementMode.cs` and handle it in `DesignerViewModel`.
5. Add property fields to `PropertiesViewModel` and bind them in `PropertiesPaneView.xaml`.

---

## Branch conventions

Use short descriptive branches. No formal convention is documented — follow the existing pattern:

- `feat/<topic>` for features
- `fix/<topic>` for bug fixes
- `docs/<topic>` for documentation changes

The default branch is `main`.

---

## PR process

1. Branch from `main`, make focused changes.
2. Ensure `dotnet build LabelDesigner.slnx` succeeds with no new warnings.
3. Ensure `dotnet test LabelDesigner.Tests` passes.
4. Open a PR against `main` with a clear description of the change.
5. Address review feedback; squash-merge when approved.

---

## Key architectural documents

- [ARCHITECTURE.md](ARCHITECTURE.md) — component diagram, DI registration, data flow
- [CONTEXT.md](../CONTEXT.md) — domain vocabulary; use consistent terminology in code and PRs
- [`docs/adr/`](adr/) — records for major design decisions
