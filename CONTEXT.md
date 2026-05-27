# LabelDesigner — Domain Context

This document defines the user-facing language for LabelDesigner and records key architectural decisions so that plan discussions, module names, and agent briefs use consistent terminology.

---

## Language

**Label**
The user-facing design artifact that is edited, previewed, printed, and saved.
_Avoid_: Scene Document, document

**Scene Document**
The in-memory representation of one **Label**.
_Avoid_: Label, file

**Label Editing**
The application-level behaviour that mutates a **Label** through operations like placement, copy, alignment, and ordering.
_Avoid_: Designer Commands, Scene Graph Editing

**Sheet Layout**
The saved arrangement of one or more **Labels** on a physical page, including rows, columns, gap, and margin.
_Avoid_: Temporary print setting, ad hoc tiling

**Physical Sheet**
The real output page used for print or PDF, which may contain one or more **Labels**.
_Avoid_: Canvas page, label page

**Label Stock Preset**
A named preset that fills a **Sheet Layout** for a known physical label stock (e.g. Avery 3x8).
_Avoid_: Hard-coded sheet template, ad hoc stock setting

**Layer**
A named grouping within a **Scene Document** that holds one or more **Elements**. Layers have independent visibility and lock state.
_Avoid_: group, z-group

**Element**
One design object on a **Label** — Barcode, Text, Shape, Line, or Image.
_Avoid_: object, item, widget

**Guide**
A non-printing reference line (horizontal or vertical) positioned in mm, placed by dragging from the ruler edge. Guides help align elements and persist across saves.
_Avoid_: guide line, ruler mark, measurement line

**Viewport**
The canvas coordinate space — tracks zoom, pan offset, and the pixel position of the page origin so rulers read in document units (mm/cm/in) from the page top-left corner.
_Avoid_: camera, transform

---

## Relationships

- A **Label** is represented in memory by exactly one **Scene Document**.
- A **Scene Document** contains one or more **Layers**; each **Layer** holds zero or more **Elements**.
- **Label Editing** changes a **Label** by mutating its **Scene Document** through the undo stack.
- A **Label** may include one **Sheet Layout** that controls how it is printed or exported onto a **Physical Sheet**.
- A **Sheet Layout** places one or more **Labels** onto a **Physical Sheet**.
- A **Label Stock Preset** populates a **Sheet Layout** for a **Physical Sheet**.
- The **Viewport** maps **Scene Document** coordinates to screen pixels and drives ruler labels.

---

## Implemented capabilities (as of May 2026)

| Capability | Status |
|---|---|
| Scene graph (layers, elements, undo) | ✅ |
| Element types: Barcode, Text, Shape, Line, Image | ✅ |
| Interaction: select, move, resize, rotate, multi-select | ✅ |
| Rulers with page-origin 0,0 and unit switching | ✅ |
| Snap to grid (toggle + configurable size) | ✅ |
| Ruler guides: create, persist, visualize | ✅ |
| Layers panel: named layers, eye/lock, add/delete, element list | ✅ |
| Properties panel: all element types with undo integration | ✅ |
| Barcode text label: independent font/size/color | ✅ |
| Text: multiline, font family/size/bold/italic, alignment | ✅ |
| Undo / redo | ✅ |
| Copy / paste (clone at copy) | ✅ |
| Zoom / pan | ✅ |
| Page templates | ✅ |
| Canvas performance: barcode bitmap cache, smart Invalidate | ✅ |
| Export: PDF, PNG | ✅ |
| Print preview | ✅ |
| Direct print | ✅ |
| Multi-label sheet layout (rows × columns) | ✅ |
| CSV data binding / merge print | ✅ |
| Settings dialog (theme, units, snap) | ✅ |
| Document save / load (JSON) | ✅ |

---

## Architectural decisions

### ADR-0001 — Draw-command rendering seam
See [`docs/adr/0001-draw-command-rendering-seam.md`](docs/adr/0001-draw-command-rendering-seam.md).

The renderer emits an intermediate list of draw commands rather than painting directly. This allows canvas, print preview, PDF, and PNG export to share one rendering path.

### ADR-0002 — Coordinate system and DPI scaling
See [`docs/adr/0002-coordinate-system-and-dpi-scaling.md`](docs/adr/0002-coordinate-system-and-dpi-scaling.md).

All `DesignElement.Bounds` are stored in **screen pixels at actual device DPI** — not logical 96-DPI pixels. `PixelsPerMm = GetDpiForWindow(hwnd) / 25.4`. Every export path (print, PDF, PNG) derives its scale from this value. Hardcoding 96 DPI causes blank output on HiDPI monitors.

```
canvas_pixels  = mm × PixelsPerMm
print scale    = printDpi / (PixelsPerMm × 25.4)
pdf pointsPx   = 72 / (PixelsPerMm × 25.4)
```

### ADR-0003 — Syncfusion ribbon async re-entrancy
See [`docs/adr/0003-syncfusion-ribbon-async-reentrancy.md`](docs/adr/0003-syncfusion-ribbon-async-reentrancy.md).

Syncfusion ribbon buttons cannot bind `AsyncRelayCommand`. All async ribbon actions use a sync `RelayCommand` wrapper (`_ = MethodAsync()`). Any such method that opens UI (dialog, picker) before its first natural await **must** begin with `await Task.Yield()` to avoid calling into WinUI modal APIs while Syncfusion's click handler is still on the call stack.

### ADR-0004 — Ruler guides
See [`docs/adr/0004-ruler-guides.md`](docs/adr/0004-ruler-guides.md).

**Guides** are placed by dragging from ruler edges and stored in `SceneDocument.Guides` as mm-based line definitions. Guides are rendered as dashed reference lines but do not print. Snap feedback during element drag shows temporary guides for alignment hints.

### CanvasViewport origin
`CanvasViewport.PageOriginX/Y` stores the pixel position of the page top-left corner on screen. Ruler labels subtract this origin before converting pixels to document units, so the ruler reads `0 mm` at the page corner regardless of pan or zoom.

### Barcode caching
`ElementRenderer` holds a static `_barcodeCache` keyed by `"value|width|height"`. ZXing generation is skipped on subsequent frames for unchanged barcodes.

---

## Flagged ambiguities (resolved)

- **"document"** was used for both the artifact and the in-memory structure → resolved: **Label** (artifact) vs **Scene Document** (in-memory).
- **"Layer"** was colloquially used to mean z-order, not a named grouping → resolved: a **Layer** is an explicit named container in the scene graph; z-order is implicit from layer index × element index.
- **"page"** was ambiguous between the label canvas and the output sheet → resolved: **Label** (design surface), **Physical Sheet** (output page).

---

## Example dialogue

> **Dev:** "When a user opens a **Label**, are we loading the whole **Scene Document**?"
> **Domain expert:** "Yes — the **Label** is what the user thinks about, and the **Scene Document** is the in-memory shape we edit."

> **Dev:** "If the same **Label** is printed several times on one sheet, where is that defined?"
> **Domain expert:** "In the **Sheet Layout** saved with the **Label**, because that arrangement is part of the intended stock geometry."

> **Dev:** "How do we support Avery-style sheets without rebuilding the layout each time?"
> **Domain expert:** "With a **Label Stock Preset** that fills the **Sheet Layout** for that known stock."
