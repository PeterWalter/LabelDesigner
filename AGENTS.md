# LabelDesigner — Architecture & Design Document

## Project Overview
A WinUI 3 desktop application for WYSIWYG label and barcode design, targeting
direct-to-printer output and PDF export. No database. File-based persistence.
Clean Architecture + MVVM + .NET 10.

---

## 1. Competitive Landscape

### BarTender (Seagull Scientific) — Industry Standard
- WYSIWYG label canvas with drag-drop placement
- 100+ barcode symbologies, RFID encoding
- Database/data-source binding (Excel, SQL, CSV, JSON)
- Intelligent templates, conditional printing, serialization
- Layers, z-order, rotation, alignment tools
- Print automation engine, print preview, PDF/PNG export
- Ruler guides, snap-to-grid, snap-to-object
- Undo/redo, multi-select, grouping

### NiceLabel / LabelView
- Same space as BarTender — WYSIWYG, data binding, compliance labeling
- Strong on label management systems, cloud integration
- Template versioning, approval workflows

### Canva
- General 2D design tool — not label-specific but excellent UX model
- Infinite canvas, zoom/pan, drag-drop elements
- Grid/ruler guides, snap-to-grid, alignment snapping
- SVG import/export, PDF export, print
- Layer panel, properties panel, color picker
- Template marketplace

### Label Live
- Real-time label preview, cloud-connected
- Template-based rapid design
- Web and desktop

### Key Takeaways for Us
| Capability | BarTender | Canva | Our Target |
|---|---|---|---|
| WYSIWYG canvas | ✓ | ✓ | ✓ |
| Rulers + snap | ✓ | ✓ | ✓ (already done) |
| Multi-element types | ✓ | ✓ | ✓ (plan) |
| Layers / z-order | ✓ | ✓ | ✓ (plan) |
| Rotation | ✓ | ✓ | planned |
| Data binding | ✓ | limited | planned |
| Print direct | ✓ | ✓ | ✓ (plan) |
| PDF export | ✓ | ✓ | ✓ (plan) |
| SVG support | partial | ✓ | ✓ (plan) |
| Undo/redo | ✓ | ✓ | planned |
| Multi-select | ✓ | ✓ | planned |
| Zoom/pan | ✓ | ✓ | planned |
| File format | proprietary | cloud | open JSON |

---

## 2. Current Codebase Assessment

### What's There
```
LabelDesigner.slnx
├── LabelDesigner.Core/              ← Domain models (net10.0)
│   ├── Models/    DesignElement, BarcodeElement, TextElement, GuideLine
│   ├── Enums/     BarcodeTextPosition, PageSize, ResizeHandle
│   └── ValueObjects/  RectD
├── LabelDesigner.Application/       ← Application layer (net10.0)
│   └── (empty folders: Commands, DTOs, Features, Services, ViewModels)
├── LabelDesigner.Infrastructure/    ← Infrastructure (net10.0-windows)
│   ├── Interfaces/     IBarcodeService, IRenderService
│   ├── Barcode/        BarcodeService (ZXing)
│   ├── Rendering/      RenderService (Win2D CanvasDrawingSession)
│   ├── Services/       SnapService
│   └── Common/         RectExtensions
├── LabelDesigner.App/              ← WinUI 3 App (net10.0-windows)
│   ├── Views/          DesignerCanvasView.xaml(.cs)
│   ├── Controls/       RulerControl.xaml(.cs), RibbonControl.xaml(.cs)
│   ├── ViewModels/     MainViewModel, DesignerViewModel, RibbonViewModel, PropertiesViewModel
│   ├── Resources/      Styles.xaml
│   └── App.xaml(.cs)   DI via Microsoft.Extensions.Hosting
└── LabelDesigner.Tests/            ← xUnit (net10.0)
```

### NuGet Dependencies
- `CommunityToolkit.Mvvm` 8.4.2 — MVVM source generators
- `CommunityToolkit.WinUI.Converters` — value converters
- `Microsoft.Graphics.Win2D` 1.4.0 — GPU-accelerated 2D canvas
- `Syncfusion.Ribbon.WinUI` 33.2.3 — Office-style ribbon
- `Syncfusion.Pdf.Net.Core` 33.2.3 — PDF generation (already referenced!)
- `ZXing.Net` 0.16.11 — barcode generation
- `CsvHelper` 33.1.0 — CSV data binding (already referenced!)
- `Microsoft.Extensions.Hosting` — DI container

### What's Missing (vs competition)
1. **Element types** — only Barcode + Text; need Image, Shape, Line, Container
2. **Scene graph** — no z-order, layering, or grouping
3. **Print** — no print pipeline at all
4. **PDF export** — Syncfusion PDF referenced but no export service
5. **SVG** — no SVG import or rendering
6. **File persistence** — no save/load of labels
7. **Undo/redo** — absent
8. **Rotation** — absent
9. **Multi-select, copy/paste** — absent
10. **Zoom/pan** — canvas fixed at 1:1
11. **Scrolling rulers** — rulers don't track viewport
12. **Data binding** — static values only
13. **Label stock templates** — no predefined sizes
14. **Properties panel** — stub only

---

## 3. Architecture Vision

### Principles
- **No database.** Labels are files. Persistence is JSON on disk.
- **Clean Architecture.** Dependencies flow inward: App → Application → Core ← Infrastructure.
- **MVVM all the way.** No code-behind logic. ViewModels own all state.
- **Scene graph** for the 2D design surface — a tree of elements with transforms.
- **SVG as first-class citizen** for shapes, icons, and export.
- **Print directly** via Windows print subsystem + Win2D for raster output.
- **PDF export** via Syncfusion PDF (already in dependencies).
- **Plugin-ready** — barcode symbologies, export formats, data sources via interfaces.

### Layer Responsibilities

```
┌──────────────────────────────────────────────────────┐
│  App (WinUI 3)                                       │
│  Views, Controls, XAML bindings, DI composition root │
├──────────────────────────────────────────────────────┤
│  Application                                         │
│  Use-cases, commands, DTOs, feature orchestration    │
├──────────────────────┬───────────────────────────────┤
│  Core (pure .NET)    │  Infrastructure               │
│  Domain models       │  RenderService (Win2D + SVG)  │
│  Value objects       │  BarcodeService (ZXing)       │
│  Enums               │  PrintService (Windows.Graphics.Printing) │
│  Interfaces          │  PdfExportService (Syncfusion)│
│                      │  SvgService (Svg.Skia)        │
│                      │  PersistenceService (JSON)    │
└──────────────────────┴───────────────────────────────┘
```

### DI Registration (in App.ConfigureServices)
```csharp
// ViewModels
services.AddSingleton<MainViewModel>();
services.AddSingleton<DesignerViewModel>();
services.AddSingleton<RibbonViewModel>();
services.AddSingleton<PropertiesViewModel>();

// Core services (interfaces in Core, implementations in Infrastructure)
services.AddSingleton<IBarcodeService, BarcodeService>();
services.AddSingleton<IRenderService, RenderService>();
services.AddSingleton<IPrintService, PrintService>();
services.AddSingleton<IPdfExportService, PdfExportService>();
services.AddSingleton<ISvgService, SvgService>();
services.AddSingleton<ILabelPersistenceService, JsonLabelPersistenceService>();
services.AddSingleton<IUndoRedoService, UndoRedoService>();
services.AddSingleton<ISceneGraphService, SceneGraphService>();
```

---

## 4. 2D Scene Graph Design

### Why a Scene Graph?
A label is a 2D document. Unlike a flat list of elements, a scene graph:
- Supports z-ordering (layers)
- Supports grouping (containers)
- Supports transforms (rotation, scale, translate)
- Enables hit-testing by traversal order
- Maps naturally to SVG's `<g>` and `<svg>` structure

### Element Hierarchy
```
SceneGraph
├── PageNode (the label page — size, orientation, margins)
│   ├── LayerNode (named layer, e.g., "Background", "Data", "Overlay")
│   │   ├── BarcodeElement
│   │   ├── TextElement
│   │   ├── ImageElement
│   │   ├── ShapeElement (rect, ellipse, polygon, path)
│   │   ├── LineElement
│   │   ├── ContainerElement (group with local transform)
│   │   │   ├── TextElement
│   │   │   └── BarcodeElement
│   │   └── ...
│   └── LayerNode ...
```

### Element Model (Core)
```csharp
// Base — all elements share these
public abstract class DesignElement
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public RectD Bounds { get; set; }           // local bounds
    public double Rotation { get; set; }         // degrees
    public double Opacity { get; set; } = 1.0;
    public bool Locked { get; set; }
    public bool Visible { get; set; } = true;
    public Dictionary<string, string> Metadata { get; } = new(); // extensible

    // Transform matrix (computed from Bounds + Rotation)
    public Matrix3x2 GetLocalTransform();
}

// Concrete elements
public class BarcodeElement : DesignElement
{
    public string Value { get; set; }
    public BarcodeFormat Format { get; set; }      // not just CODE_128
    public BarcodeTextPosition TextPosition { get; set; }
    public string TextFormat { get; set; }          // e.g., "{0:d}" for dates
    // Data-bound fields
    public string? DataSourceField { get; set; }
}

public class TextElement : DesignElement
{
    public string Text { get; set; }
    public string FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontWeight Weight { get; set; }
    public FontStyle Style { get; set; }
    public TextAlignment Alignment { get; set; }
    public Color Foreground { get; set; }
    public bool WordWrap { get; set; }
    public string? DataSourceField { get; set; }
}

public class ImageElement : DesignElement
{
    public string SourcePath { get; set; }          // file path or embedded
    public StretchMode Stretch { get; set; }
}

public class ShapeElement : DesignElement
{
    public ShapeType Type { get; set; }             // Rectangle, Ellipse, Polygon, Path
    public string? PathData { get; set; }           // SVG path data
    public Color Fill { get; set; }
    public Color Stroke { get; set; }
    public double StrokeWidth { get; set; }
}

public class LineElement : DesignElement
{
    public double X1, Y1, X2, Y2;
    public Color Stroke { get; set; }
    public double StrokeWidth { get; set; }
}

public class ContainerElement : DesignElement
{
    public List<DesignElement> Children { get; } = new();
}
```

### Scene Graph Service (Application layer)
```csharp
public interface ISceneGraphService
{
    // Tree management
    SceneDocument CurrentDocument { get; }
    void Load(SceneDocument doc);
    void AddElement(DesignElement element, Guid? parentLayer = null);
    void RemoveElement(Guid id);
    void MoveElement(Guid id, Guid newParent);
    void ReorderElement(Guid id, int newZIndex);

    // Query
    DesignElement? HitTest(Point p);          // topmost hit
    IReadOnlyList<DesignElement> HitTestAll(Point p); // all hits
    IEnumerable<DesignElement> GetElementsInRect(RectD rect);
    IReadOnlyList<DesignElement> SelectedElements { get; }

    // Selection
    void Select(Guid id);
    void Deselect(Guid id);
    void SelectAll();
    void ClearSelection();

    // Transforms
    void MoveSelected(double dx, double dy);
    void ResizeSelected(ResizeHandle handle, double dx, double dy);
    void RotateSelected(double angle);

    // Undo
    IUndoRedoService UndoRedo { get; }
}
```

---

## 5. Rendering Pipeline

### Dual Renderer Strategy
Win2D handles the interactive canvas (GPU-accelerated). SVG can be rendered
in two ways:

1. **Design-time (canvas):** Win2D rasterizes SVG to a `CanvasBitmap` via `Svg.Skia`
   and draws it as an image. This gives pixel-perfect preview at full speed.
2. **Export (PDF/print):** SVG elements are passed through natively when the
   export target supports vector output, preserving infinite resolution.

### IRenderService (revised)
```csharp
public interface IRenderService
{
    // Main draw call — iterates scene graph, draws to Win2D
    void RenderScene(
        CanvasDrawingSession ds,
        SceneDocument document,
        RectD viewport,        // scroll offset + zoom
        float zoom);

    // Rasterize a single element to a SoftwareBitmap (for thumbnails)
    SoftwareBitmap RenderElement(DesignElement element, float dpi);

    // Offscreen render (for print/PDF — returns raster at target DPI)
    SoftwareBitmap RenderPage(SceneDocument document, float dpi);
}
```

### Draw Order
```
1. Page background (white)
2. Grid (if visible, zoom-aware)
3. Margin guides (dashed)
4. Page outline
5. Elements in z-order (Layer → Element → Children)
   - For each element:
     a. Apply local transform (translation + rotation)
     b. Draw element body (barcode image, text, shape fill, etc.)
     c. Draw selection handles (if selected)
6. Alignment guides (temporary snap lines)
7. Ruler ticks and labels (separate control, synced to viewport)
```

### SVG Integration
```csharp
// Infrastructure/SvgService.cs
public interface ISvgService
{
    // Convert SVG file/string to renderable bitmap
    CanvasBitmap LoadSvg(CanvasDevice device, string svgFilePath, float width, float height);
    CanvasBitmap LoadSvgFromString(CanvasDevice device, string svgContent, float width, float height);

    // Convert our ShapeElement to SVG path string
    string ToSvgPath(ShapeElement shape);

    // Convert our entire document to standalone SVG
    string ExportToSvg(SceneDocument document);
}
```

NuGet: `Svg.Skia` (MIT, cross-platform, .NET-native SVG rendering via SkiaSharp).

For the Win2D integration, render Svg.Skia to a `SKBitmap`, then copy pixels
to a `CanvasBitmap`. This is cost-effective since it only happens on load/change.

---

## 6. Print Pipeline

### Direct-to-Printer
Windows provides `Windows.Graphics.Printing.PrintManager` for WinUI 3 apps.

```csharp
public interface IPrintService
{
    Task PrintAsync(SceneDocument document);
    Task ShowPrintPreviewAsync(SceneDocument document);
}

// Implementation flow:
// 1. Register with PrintManager
// 2. On PrintTaskRequested, create a PrintTask
// 3. For each page: render the document at 300 DPI using IRenderService.RenderPage
// 4. Submit raster page to PrintTask
```

The document renders to a high-DPI `SoftwareBitmap` (via Win2D offscreen),
which gets submitted as a raster page. This is the standard approach —
even BarTender rasterizes for the Windows print pipeline.

### Label-Specific Print Considerations
- Page size must match label stock (not A4 by default)
- Support for continuous-feed printers (no page breaks)
- Printer calibration (offsets, scaling)
- Print quantity and serialization

---

## 7. PDF Export

Already have `Syncfusion.Pdf.Net.Core` referenced. Use it to:

```csharp
public interface IPdfExportService
{
    Task ExportAsync(SceneDocument document, string outputPath, PdfExportOptions options);
}

public class PdfExportOptions
{
    public bool VectorGraphics { get; set; } = true;  // use vector where possible
    public bool EmbedFonts { get; set; } = true;
    public float Dpi { get; set; } = 300;
}
```

For vector-capable exports, shapes, lines, and text are written as native PDF
vector operators. Barcodes and images are embedded as raster. This is the
same approach BarTender uses.

---

## 8. File Format — `.ldlabel`

No database. A single JSON file containing the scene graph.

```json
{
  "version": "1.0",
  "format": "ldlabel",
  "document": {
    "pageSize": "A4",
    "landscape": false,
    "widthMm": 100,
    "heightMm": 50,
    "margins": { "left": 2, "top": 2, "right": 2, "bottom": 2 },
    "dpi": 300,
    "dataSource": { "type": "csv", "path": "data/products.csv" }
  },
  "elements": [
    {
      "type": "BarcodeElement",
      "id": "guid-here",
      "name": "Product Barcode",
      "bounds": { "x": 10, "y": 5, "w": 80, "h": 30 },
      "rotation": 0,
      "value": "{{ProductCode}}",
      "format": "CODE_128",
      "textPosition": "Bottom"
    },
    {
      "type": "TextElement",
      "id": "guid-here",
      "name": "Product Name",
      "bounds": { "x": 10, "y": 37, "w": 80, "h": 8 },
      "text": "{{ProductName}}",
      "fontSize": 8,
      "fontFamily": "Arial"
    }
  ],
  "layers": [
    { "name": "Default", "visible": true, "locked": false }
  ]
}
```

### Persistence Service
```csharp
public interface ILabelPersistenceService
{
    Task<SceneDocument> LoadAsync(string filePath);
    Task SaveAsync(SceneDocument document, string filePath);
    Task<SceneDocument> ImportFromJsonAsync(string json);
    Task<string> ExportToJsonAsync(SceneDocument document);
}
```

---

## 9. Data Binding

Labels often print variable data (different text/barcode per label). Sources:
CSV files (already have CsvHelper), Excel (via Syncfusion XlsIO, already referenced),
or simple JSON arrays.

```csharp
public interface IDataSourceService
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> LoadAsync(string path);
}

// Elements reference fields with {{FieldName}} syntax
// DesignerViewModel resolves these at print/export time.
```

---

## 10. Undo/Redo

Command pattern. Every mutation goes through the scene graph service, which
records an undo entry.

```csharp
public interface IUndoRedoService
{
    void Execute(IUndoableCommand command);
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Undo();
    void Redo();
    void Clear();
}

public interface IUndoableCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}
```

---

## 11. Canvas UX — Zoom, Pan, Ruler Sync

### Viewport Model
```csharp
public class CanvasViewport : ObservableObject
{
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Zoom { get; set; } = 1.0;   // 0.25 … 4.0

    public Point ScreenToWorld(Point screen);
    public Point WorldToScreen(Point world);
}
```

The `DesignerCanvasView` applies a Win2D transform before drawing:
```csharp
ds.Transform = Matrix3x2.CreateTranslation(-OffsetX, -OffsetY)
             * Matrix3x2.CreateScale(Zoom);
```

Ruler controls subscribe to `CanvasViewport` property changes and redraw
their ticks based on current offset + zoom.

### Pan: Middle-mouse drag or hold Space + drag
### Zoom: Ctrl + scroll wheel (centered on cursor)

---

## 12. UI Layout (MainWindow)

```
┌─────────────────────────────────────────────────────────┐
│  Ribbon (Syncfusion)                                    │
│  [Home] [Insert] [View]  + contextual tabs              │
├────┬────────────────────────────────────────────────────┤
│    │  Top Ruler (scrolls with canvas offset)            │
│    ├────────────────────────────────────────────────────┤
│Left│                                                    │
│Rul.│           DESIGN CANVAS                            │
│    │     (page outline, grid, elements)                 │
│    │                                                    │
│    │                                                    │
├────┴────────────────────────────────────────────────────┤
│  Status Bar: page size │ zoom % │ cursor pos │ element count │
├──────────────────────────────────────────────────────────┤
│  Properties Panel (right dock)                          │
│  Name: __________                                      │
│  X: ___  Y: ___  W: ___  H: ___                        │
│  Font: ___  Size: ___  Color: [■]                      │
│  Data Field: __________                                 │
└──────────────────────────────────────────────────────────┘
```

### Toolbox (Insert tab)
```
┌─────────────┐
│  Pointer     │
│  Barcode     │
│  Text        │
│  Line        │
│  Rectangle   │
│  Ellipse     │
│  Image       │
│  Container   │
│  SVG Shape   │
└─────────────┘
```

---

## 13. Implementation Phases (Suggested Order)

### Phase 1 — Foundation (where we are now → solid core)
- [ ] Complete scene graph model (Core.Models additions)
- [ ] ISceneGraphService + implementation
- [ ] Undo/redo via command pattern
- [ ] JSON file persistence (.ldlabel)
- [ ] New/open/save commands in Ribbon
- [ ] Ruler synced to viewport offset

### Phase 2 — Element Richness
- [ ] ImageElement + rendering
- [ ] ShapeElement (rect, ellipse) + rendering
- [ ] LineElement + rendering
- [ ] ContainerElement (group) + rendering
- [ ] Rotation handle + transform rendering
- [ ] Multi-select (Shift+click, rubber-band)

### Phase 3 — Canvas UX
- [ ] Zoom/pan (CanvasViewport)
- [ ] Middle-mouse pan, Ctrl+scroll zoom
- [ ] Zoom-to-fit, zoom-to-page
- [ ] Scrolling rulers

### Phase 4 — Print & Export
- [ ] IPrintService (Windows print pipeline)
- [ ] IPdfExportService (Syncfusion)
- [ ] SVG export (ISvgService.ExportToSvg)
- [ ] PNG/JPG export

### Phase 5 — Data & Advanced
- [ ] Data binding (CSV, Excel via CsvHelper + Syncfusion XlsIO)
- [ ] Serialization (incrementing barcode values)
- [ ] Label stock templates (predefined sizes)
- [ ] Svg.Skia integration for SVG import
- [ ] Copy/paste, duplicate, align/distribute
- [ ] Properties panel live editing

### Phase 6 — Polish
- [ ] Print preview dialog
- [ ] Template gallery
- [ ] Dark theme
- [ ] Keyboard shortcuts
- [ ] Accessibility (Narrator support)

---

## 14. Build & Test Commands

```powershell
# Restore all projects
dotnet restore LabelDesigner.slnx

# Build (Debug, x64)
dotnet build LabelDesigner.slnx --configuration Debug

# Run tests
dotnet test LabelDesigner.Tests/LabelDesigner.Tests.csproj

# Publish (self-contained, trimmed)
dotnet publish LabelDesigner.App/LabelDesigner.App.csproj `
    --configuration Release `
    -r win-x64 `
    --self-contained true
```

---

## 15. Coding Conventions

- **Namespaces:** `LabelDesigner.{Layer}.{Category}` — e.g., `LabelDesigner.Core.Models`
- **Models:** immutable where practical; use `init` accessor
- **ViewModels:** `partial class` with CommunityToolkit.Mvvm source generators
- **Services:** interface in Core (or Application), implementation in Infrastructure
- **DI:** singleton for stateful services, transient for stateless
- **Tests:** xUnit, one test project, mirror Core structure
- **Nullability:** enabled everywhere (`<Nullable>enable</Nullable>`)
- **No regions.** Group members by type (properties, constructor, commands, methods)
- **Async:** use `Task` / `Task<T>` for I/O; fire-and-forget for UI commands

---

## 16. Implementation Plan

### Phase 1 — Foundation (scene graph, undo/redo, file persistence, viewport)

**Goal:** Replace the flat `ObservableCollection<DesignElement>` with a proper
scene graph, add undo/redo and file save/load, and introduce zoom/pan with
rulers that track the viewport.

```
Files changed/added:
  Core/
    Models/
      DesignElement.cs          ← extend: Id, Name, Rotation, Opacity, Locked, Visible, Metadata
      ImageElement.cs           ← NEW
      ShapeElement.cs           ← NEW
      LineElement.cs            ← NEW
      ContainerElement.cs       ← NEW
      SceneDocument.cs          ← NEW (page + layers + elements tree)
      LayerNode.cs              ← NEW
      PageNode.cs               ← NEW
    Enums/
      ShapeType.cs              ← NEW (Rectangle, Ellipse, Polygon, Path)
      BarcodeFormat.cs          ← NEW (wraps ZXing.BarcodeFormat)
    Interfaces/                 ← NEW folder
      ISceneGraphService.cs     ← NEW (defined in Core, implemented in Application)
      IUndoRedoService.cs       ← NEW
      ILabelPersistenceService.cs ← NEW
      IDataSourceService.cs     ← NEW
      IPrintService.cs          ← NEW
      IPdfExportService.cs      ← NEW
      ISvgService.cs            ← NEW
  Application/
    Services/
      SceneGraphService.cs      ← NEW (tree management, hit-test, selection)
    Commands/
      IUndoableCommand.cs       ← NEW
      UndoRedoService.cs        ← NEW
  Infrastructure/
    Persistence/
      JsonLabelPersistenceService.cs ← NEW (System.Text.Json serialization)
  App/
    ViewModels/
      DesignerViewModel.cs      ← REFACTOR: use ISceneGraphService, undo/redo, New/Open/Save
      CanvasViewport.cs         ← NEW (OffsetX, OffsetY, Zoom, ScreenToWorld, WorldToScreen)
    Views/
      DesignerCanvasView.xaml(.cs) ← REFACTOR: viewport transform, zoom/pan, coords
    Controls/
      RulerControl.xaml(.cs)    ← REFACTOR: subscribe to viewport, scroll ticks
    App.xaml.cs                 ← EXTEND: register new DI services
```

#### Step-by-step order

**1. Core domain model extensions**

File: `LabelDesigner.Core/Models/DesignElement.cs`
- Add `Guid Id { get; init; } = Guid.NewGuid()`
- Add `string Name { get; set; } = ""`
- Add `double Rotation { get; set; }` (degrees)
- Add `double Opacity { get; set; } = 1.0`
- Add `bool Locked { get; set; }`
- Add `bool Visible { get; set; } = true`
- Add `Dictionary<string, string> Metadata { get; }`
- Add `Guid? ParentId { get; set; }` (scene graph parent)
- Add `int ZIndex { get; set; }` (draw order within parent)
- Add `System.Numerics.Matrix3x2 GetLocalTransform()` method

**1b. New element types**

File: `LabelDesigner.Core/Models/ImageElement.cs`
```csharp
public class ImageElement : DesignElement
{
    public string SourcePath { get; set; } = "";
    public ImageStretch Stretch { get; set; } = ImageStretch.Uniform;
}
```

File: `LabelDesigner.Core/Models/ShapeElement.cs`
```csharp
public class ShapeElement : DesignElement
{
    public ShapeType Type { get; set; }
    public string? PathData { get; set; }
    public string Fill { get; set; } = "#000000";
    public string Stroke { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 1;
}
```

File: `LabelDesigner.Core/Models/LineElement.cs`
```csharp
public class LineElement : DesignElement
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; } = 100;
    public double Y2 { get; set; } = 0;
    public string Stroke { get; set; } = "#000000";
    public double StrokeWidth { get; set; } = 1;
}
```

File: `LabelDesigner.Core/Models/ContainerElement.cs`
```csharp
public class ContainerElement : DesignElement
{
    public List<Guid> ChildIds { get; } = new();
}
```

**1c. Scene graph nodes**

File: `LabelDesigner.Core/Models/SceneDocument.cs`
```csharp
public class SceneDocument
{
    public string Version { get; set; } = "1.0";
    public PageNode Page { get; set; } = new();
    public List<LayerNode> Layers { get; } = new();
    public List<DesignElement> AllElements { get; } = new();
    public DataSourceConfig? DataSource { get; set; }
}

public class PageNode
{
    public double WidthMm { get; set; } = 100;
    public double HeightMm { get; set; } = 50;
    public double Dpi { get; set; } = 300;
    public Margins Margins { get; set; } = new();
}

public class LayerNode
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Layer";
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public List<Guid> ElementIds { get; } = new();
}

public record Margins(double Left = 2, double Top = 2, double Right = 2, double Bottom = 2);

public class DataSourceConfig
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
}
```

New enums:
- `LabelDesigner.Core/Enums/ShapeType.cs` — `Rectangle, Ellipse, Polygon, Path`
- `LabelDesigner.Core/Enums/ImageStretch.cs` — `None, Fill, Uniform, UniformToFill`
- `LabelDesigner.Core/Enums/BarcodeFormat.cs` — maps ZXing formats

**2. Core interfaces** (in `LabelDesigner.Core/Interfaces/`)

```csharp
// ISceneGraphService.cs
public interface ISceneGraphService
{
    SceneDocument CurrentDocument { get; }
    void Load(SceneDocument doc);
    void Clear();

    void AddElement(DesignElement element, Guid? parentLayerId = null);
    void RemoveElement(Guid id);
    void MoveElement(Guid id, Guid newParentLayerId);
    void ReorderElement(Guid id, int newZIndex);

    DesignElement? GetElement(Guid id);
    IReadOnlyList<DesignElement> GetLayerElements(Guid layerId);
    DesignElement? HitTest(Point p);
    IReadOnlyList<DesignElement> HitTestAll(Point p);
    IEnumerable<DesignElement> GetElementsInRect(RectD rect);

    IReadOnlyList<Guid> SelectedIds { get; }
    DesignElement? SingleSelected { get; }
    void Select(Guid id);
    void Deselect(Guid id);
    void ToggleSelect(Guid id);
    void SelectAll();
    void ClearSelection();

    void MoveSelected(double dx, double dy);
    void ResizeSelected(ResizeHandle handle, double dx, double dy);
    void RotateSelected(double angle);

    LayerNode AddLayer(string name);
    void RemoveLayer(Guid id);
    void ReorderLayer(Guid id, int newIndex);
}

// IUndoRedoService.cs
public interface IUndoRedoService
{
    void Execute(IUndoableCommand command);
    bool CanUndo { get; }
    bool CanRedo { get; }
    string? UndoDescription { get; }
    string? RedoDescription { get; }
    void Undo();
    void Redo();
    void Clear();
}

// ILabelPersistenceService.cs
public interface ILabelPersistenceService
{
    Task<SceneDocument> LoadAsync(string filePath);
    Task SaveAsync(SceneDocument document, string filePath);
    Task<SceneDocument> LoadFromJsonAsync(string json);
    Task<string> SaveToJsonAsync(SceneDocument document);
}
```

**3. Undo/Redo** — `LabelDesigner.Application/Commands/`

```csharp
// IUndoableCommand.cs
public interface IUndoableCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

// UndoRedoService.cs
public class UndoRedoService : IUndoRedoService
{
    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();

    public void Execute(IUndoableCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }
    public void Undo()
    {
        if (!_undoStack.TryPop(out var cmd)) return;
        cmd.Undo();
        _redoStack.Push(cmd);
    }
    public void Redo()
    {
        if (!_redoStack.TryPop(out var cmd)) return;
        cmd.Execute();
        _undoStack.Push(cmd);
    }
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;
    public string? UndoDescription => _undoStack.TryPeek(out var c) ? c.Description : null;
    public string? RedoDescription => _redoStack.TryPeek(out var c) ? c.Description : null;
    public void Clear() { _undoStack.Clear(); _redoStack.Clear(); }
}
```

**4. SceneGraphService** — `LabelDesigner.Application/Services/SceneGraphService.cs`

Key design:
- `Dictionary<Guid, DesignElement>` for O(1) lookup
- `Dictionary<Guid, LayerNode>` for layer management
- `HashSet<Guid>` for selected IDs
- Hit-test walks layers front-to-back, elements by ZIndex descending
- Every mutation creates an `IUndoableCommand` and calls `_undoRedo.Execute(cmd)`
- `MoveSelected(dx, dy)` applies SnapService for grid/alignment snapping

**5. JSON persistence** — `LabelDesigner.Infrastructure/Persistence/JsonLabelPersistenceService.cs`

Uses `System.Text.Json` with source generator for AOT/trimmed safety.
Serializes `SceneDocument` → `.ldlabel` JSON.
Element type discrimination via `"type"` discriminator property.

**6. CanvasViewport** — `LabelDesigner.App/ViewModels/CanvasViewport.cs`

```csharp
public partial class CanvasViewport : ObservableObject
{
    [ObservableProperty] private double _offsetX;
    [ObservableProperty] private double _offsetY;
    [ObservableProperty] private double _zoom = 1.0;
    public const double MinZoom = 0.25;
    public const double MaxZoom = 4.0;
    public Point ScreenToWorld(Point screen) =>
        new((screen.X + OffsetX) / Zoom, (screen.Y + OffsetY) / Zoom);
    public Point WorldToScreen(Point world) =>
        new(world.X * Zoom - OffsetX, world.Y * Zoom - OffsetY);
}
```

**7. IRenderService update**

New signature:
```csharp
void RenderScene(CanvasDrawingSession ds, SceneDocument document,
    IReadOnlyList<Guid> selectedIds, float zoom, RectD viewport);
```
Draw order: page background → grid → margins → page outline → elements by z-order → guides.
Viewport transform applied first: `ds.Transform = Matrix3x2.CreateTranslation(-vp.X, -vp.Y) * Matrix3x2.CreateScale(zoom)`

**8. DesignerViewModel refactor**

- Replace `ObservableCollection<DesignElement> Elements` → `ISceneGraphService Scene`
- Remove `Selected` property; use `Scene.SingleSelected`
- Add `CanvasViewport Viewport { get; }`
- Add `IUndoRedoService UndoRedo { get; }`
- Add commands: `NewDocument`, `OpenDocument`, `SaveDocument`, `Undo`, `Redo`
- `PointerPressed`: screen→world via `Viewport.ScreenToWorld`, then `Scene.HitTest`
- `PointerMoved`: world-space deltas, `Scene.MoveSelected(dx, dy)`

**9. DesignerCanvasView update**

- `OnDraw`: `_renderer.RenderScene(ds, VM.Scene.CurrentDocument, VM.Scene.SelectedIds, VM.Viewport.Zoom, ...)`
- Add `Ctrl+ScrollWheel` → zoom at cursor position
- Add middle-mouse drag → pan
- Add Space-hold for pan mode

**10. Ruler sync**

- `RulerControl` gets `Viewport` bindable property
- `OnDraw` reads `Viewport.OffsetX/Y` and `Zoom`
- Tick spacing adjusts with zoom level

**11. DI registration** — `App.xaml.cs`:

```csharp
services.AddSingleton<ISceneGraphService, SceneGraphService>();
services.AddSingleton<IUndoRedoService, UndoRedoService>();
services.AddSingleton<ILabelPersistenceService, JsonLabelPersistenceService>();
```

**Phase 1 build verification:**
```powershell
dotnet build LabelDesigner.slnx --configuration Debug
```

---

### Phase 2 — Element Richness
Shapes, images, lines, containers, rotation, multi-select. Already modeled
in Sections 4-5 above; rendered in RenderService by element type.

### Phase 3 — Canvas UX
Zoom fit-to-page, zoom slider, smooth scroll, ruler fine-tuning.

### Phase 4 — Print & Export
```csharp
// Infrastructure/Export/PrintService.cs — Windows.Graphics.Printing.PrintManager
// Infrastructure/Export/PdfExportService.cs — Syncfusion.Pdf.Net.Core
```

### Phase 5 — Data & Advanced
Data binding (CsvHelper + XlsIO), serialization, SVG import (Svg.Skia),
copy/paste, properties panel live editing.

### Phase 6 — Polish
Print preview, template gallery, dark theme, keyboard shortcuts, accessibility.

---

## 17. File Layout After Phase 1

```
LabelDesigner.slnx
├── LabelDesigner.Core/
│   ├── Enums/
│   │   ├── BarcodeFormat.cs          NEW
│   │   ├── BarcodeTextPosition.cs
│   │   ├── ImageStretch.cs           NEW
│   │   ├── PageSize.cs
│   │   ├── ResizeHandle.cs
│   │   └── ShapeType.cs              NEW
│   ├── Interfaces/                   NEW
│   │   ├── IDataSourceService.cs     NEW
│   │   ├── ILabelPersistenceService.cs NEW
│   │   ├── IPdfExportService.cs      NEW
│   │   ├── IPrintService.cs          NEW
│   │   ├── ISceneGraphService.cs     NEW
│   │   ├── ISvgService.cs            NEW
│   │   └── IUndoRedoService.cs       NEW
│   ├── Models/
│   │   ├── BarcodeElement.cs
│   │   ├── ContainerElement.cs       NEW
│   │   ├── DesignElement.cs          ↓ extended
│   │   ├── GuideLine.cs
│   │   ├── ImageElement.cs           NEW
│   │   ├── LineElement.cs            NEW
│   │   ├── SceneDocument.cs          NEW
│   │   ├── ShapeElement.cs           NEW
│   │   └── TextElement.cs
│   └── ValueObjects/
│       └── RectD.cs
├── LabelDesigner.Application/
│   ├── Commands/
│   │   ├── IUndoableCommand.cs       NEW
│   │   └── UndoRedoService.cs        NEW
│   └── Services/
│       └── SceneGraphService.cs      NEW
├── LabelDesigner.Infrastructure/
│   ├── Interfaces/
│   │   ├── IBarcodeService.cs
│   │   └── IRenderService.cs         ↓ updated
│   ├── Persistence/
│   │   └── JsonLabelPersistenceService.cs NEW
│   └── Rendering/
│       └── RenderService.cs          ↓ updated
├── LabelDesigner.App/
│   ├── Controls/
│   │   └── RulerControl.xaml(.cs)    ↓ updated
│   ├── ViewModels/
│   │   ├── CanvasViewport.cs         NEW
│   │   └── DesignerViewModel.cs      ↓ refactored
│   ├── Views/
│   │   └── DesignerCanvasView.xaml(.cs) ↓ updated
│   └── App.xaml.cs                   ↓ DI extended
└── LabelDesigner.Tests/
    └── (new tests for undo/redo, scene graph, persistence)
```

---

## 18. Key Design Decisions (Why)

| Decision | Reason |
|---|---|
| Scene graph over flat list | Enables z-order, grouping, transform hierarchy, SVG export |
| Interfaces in Core | Clean Architecture — Application and Infrastructure both depend on Core, never on each other |
| Undo via command pattern | Every mutation is atomic and reversible; stacks are simple and debuggable |
| JSON file format (.ldlabel) | No database, human-readable, versionable in git, easy to diff |
| Win2D for canvas | GPU-accelerated, built into Windows, same pipeline for screen/print/PDF |
| Syncfusion PDF for export | Already referenced, vector-capable, avoids reinventing PDF writer |
| Svg.Skia for SVG | MIT license, .NET-native, SkiaSharp under the hood, rasterizes to bitmap for Win2D |
| Singleton DI for services | Scene graph and undo/redo are document-scoped state; transient would lose context |
| System.Text.Json (source-gen) | AOT/trimmed-safe for self-contained publish; faster than Newtonsoft |