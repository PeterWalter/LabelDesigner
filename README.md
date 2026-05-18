# LabelDesigner

A professional **WinUI 3** desktop application for designing barcode labels — built for Windows with full print, PDF and PNG export.

---

## Features

### Label Canvas
- **Scene-based editing** — select, move, resize, rotate elements with handles
- **Zoom & pan** — smooth canvas zoom with mouse wheel; pan with middle-button drag
- **Rulers** — horizontal & vertical rulers with configurable units (mm / cm / in); origin (0, 0) aligned to page top-left corner
- **Snap to grid** — configurable grid size, toggle on/off via Settings
- **Undo / redo** — full command history (Ctrl+Z / Ctrl+Y)
- **Copy / paste** — element clipboard with clone-at-copy (Ctrl+C / Ctrl+V)
- **Multi-select** — rubber-band selection + Shift-click; alignment commands on selection
- **Page templates** — standard label sizes (A4, letter, custom)

### Elements
| Type | Notes |
|------|-------|
| **Barcode** | QR, Code 128, EAN-13, EAN-8, DataMatrix, PDF417, and more via ZXing.Net |
| **Text** | Multiline; font family, size, bold/italic, color, horizontal alignment |
| **Shape** | Rectangle, ellipse, rounded rectangle; fill & stroke |
| **Line** | Free-angle line with color and weight |
| **Image** | JPEG/PNG import |

### Barcode text label
- The human-readable text below a barcode has its own **font family, size, and color**, independent of the barcode symbol.

### Layers panel
- Named layers (Layer 1, Layer 2 …)
- Per-layer **visibility** toggle (eye icon) and **lock** toggle
- Expand / collapse layer to see contained elements
- Element count badge per layer
- **Add / Delete layer** toolbar
- Canvas selection synchronised — selected element highlighted in blue

### Properties panel
- Live position (X, Y), size (W, H), rotation for any selected element
- Element-specific fields: barcode value & symbology; text content, font, alignment; fill/stroke color; image source
- All edits routed through the undo stack

### Output
- **Print preview** — paginated multi-page preview with zoom
- **Direct print** — sends to any installed Windows printer; supports multiple labels per sheet
- **Multi-label sheet layout** — configure rows, columns, gap, and margin for tiled label sheets
- **Export to PDF** — full-fidelity vector PDF via SkiaSharp
- **Export to PNG** — high-resolution raster export

### Data binding
- Bind text / barcode fields to a **CSV column**
- Merge-print mode generates one page per CSV record

### Settings
- **Theme** — Light / Dark / System
- **Ruler units** — mm / cm / in
- **Snap to grid** — on/off with configurable grid size

---

## Architecture

```
LabelDesigner.App           WinUI 3 shell: views, view-models, converters
LabelDesigner.Application   Application services: scene graph, undo, file I/O, print
LabelDesigner.Core          Domain models, interfaces, value objects (no UI dependency)
LabelDesigner.Infrastructure Rendering (SkiaSharp/Win2D), export, barcode, persistence
LabelDesigner.Tests         Unit tests: scene graph, undo/redo, persistence
```

Key architectural decisions are tracked in [`docs/adr/`](docs/adr/). Domain vocabulary is in [`CONTEXT.md`](CONTEXT.md).

### Design seams
| Seam | Responsibility |
|------|---------------|
| **Interaction** | Pointer placement, rubber-band select, resize / rotate handles |
| **Label Editing** | Copy/paste, alignment, element ordering, property mutations |
| **Rendering** | Draw-command pipeline shared by canvas, print preview, PDF, and PNG |
| **Settings & Layout** | Ruler units, workspace layout, sheet layout pagination |

---

## Tech stack

| Concern | Library |
|---------|---------|
| UI framework | WinUI 3 / Windows App SDK |
| MVVM | CommunityToolkit.Mvvm |
| Canvas rendering | Microsoft.Graphics.Canvas (Win2D) |
| PDF / PNG export | SkiaSharp |
| Barcode generation | ZXing.Net |
| Unit testing | xUnit |

---

## Building

Requires **Visual Studio 2022** (or later) with the **Windows App SDK** workload installed.

```
dotnet build LabelDesigner.slnx
dotnet test  LabelDesigner.Tests
```

---

## Roadmap (next)

- SVG import (Svg.Skia)
- Right-click context menu (Cut / Copy / Paste / Bring to Front / Send to Back)
- Label Stock Presets (Avery, Dymo, Zebra standard sheets)
- Dark theme polish
- Accessibility (Narrator / keyboard-first navigation)
- Recent files list
- Unit test coverage for rendering seam and CSV data binding
