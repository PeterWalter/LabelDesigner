# ADR-0002 — Coordinate System and DPI Scaling

**Status**: Accepted  
**Date**: 2026-05-27

---

## Context

LabelDesigner renders on a HiDPI-aware WinUI 3 canvas, exports to PDF via Syncfusion, prints via Win2D rasterisation, and exports PNG. All four paths must agree on how element positions and sizes are stored, how they map to screen pixels, and how they map to physical output units (mm → points → dots).

A defect was discovered where PDF export produced blank pages and print produced blank output on monitors with scaling above 100% (≥120 DPI). The root cause was a mismatch between the coordinate unit assumed by export services and the actual unit used by the canvas.

---

## Decision

### 1 — Internal unit: screen pixels at actual screen DPI

All `DesignElement.Bounds` values (`RectD` — X, Y, Width, Height) are stored in **screen pixels at the actual device DPI of the host window**:

```
canvas_pixels = millimetres × PixelsPerMm
PixelsPerMm   = GetDpiForWindow(hwnd) / 25.4
```

On a 96 DPI monitor (100% scale): `PixelsPerMm ≈ 3.779 px/mm`  
On a 120 DPI monitor (125% scale): `PixelsPerMm ≈ 4.724 px/mm`  
On a 144 DPI monitor (150% scale): `PixelsPerMm ≈ 5.669 px/mm`

This is **not** a logical 96-DPI pixel; it is the actual physical pixel density of the display.

### 2 — DPI source of truth: `DpiService`

`LabelDesigner.App.Services.DpiService` is the sole source of screen DPI. It is initialised once per window via `DpiService.InitializeForWindow(hwnd)` and exposes:

```csharp
public static double PixelsPerMm { get; }   // = GetDpiForWindow(hwnd) / 25.4
```

Because `DpiService` is in the App layer and export services live in Infrastructure, the DPI value is **threaded as a property** into each service before use — never injected at construction time (services are singletons registered before `InitializeForWindow` is called).

### 3 — Print rasterisation scale

`PrintService` rasterises the scene to a bitmap at the target print DPI (typically 200–600 DPI). The scale factor converts from canvas pixels (screen DPI) to raster pixels (print DPI):

```
scale = printDpi / (PixelsPerMm × 25.4)
      = printDpi / screenDpi
```

The `PixelsPerMm` property on `IPrintService` must be set from `DpiService.PixelsPerMm` before each print or PNG export call:

```csharp
_printService.PixelsPerMm = DpiService.PixelsPerMm;
await _printService.PrintAsync(documents, hwnd, jobTitle);
```

**Old (broken) formula** — assumed 96 DPI hardcoded:
```csharp
float scale = dpi / 96.0f;   // Wrong on HiDPI: off by 1.25× or 1.5×
```

**Correct formula**:
```csharp
float scale = dpi / (float)(PixelsPerMm * 25.4);   // = printDpi / screenDpi
```

### 4 — PDF coordinate conversion

PDF coordinates are in **points** (1 pt = 1/72 inch). The conversion from canvas pixels to PDF points:

```
pointsPerPixel = 72 / (PixelsPerMm × 25.4)
               = 72 / screenDpi
```

`PdfExportOptions.PixelsPerMm` must be set from `DpiService.PixelsPerMm` before each PDF export:

```csharp
await _pdfExportService.ExportAsync(document, path,
    new PdfExportOptions { PixelsPerMm = DpiService.PixelsPerMm });
```

At 96 DPI: `pointsPerPixel = 72/96 = 0.75`  
At 144 DPI: `pointsPerPixel = 72/144 = 0.5`  ← **was wrongly 0.75 — elements 1.5× too large → outside page → blank PDF**

### 5 — Converting user-visible measurements (mm ↔ canvas pixels)

The Properties panel and rulers display positions and sizes in user-selected units (mm, cm, or inches). Conversions:

```
pixels → mm :  pixels / PixelsPerMm
mm → pixels :  mm × PixelsPerMm
```

`DesignerViewModel.FormatMeasurement(double pixels)` applies this conversion when displaying element properties.

---

## Consequences

- Any code that converts canvas-pixel coordinates to a physical output unit (PDF points, raster pixels, printer dots) **must** use `PixelsPerMm` — never the constant `96`.
- `IPrintService.PixelsPerMm` and `PdfExportOptions.PixelsPerMm` must be set before every export call.
- Export services are safe to be singletons; the per-call property set ensures correctness.
- Tests that use canvas-pixel values directly (e.g. `RectD(8, 8, 30, 20)`) implicitly assume the default `PixelsPerMm = 96/25.4`; this is correct for headless test execution where no window DPI is available.

---

## Related files

| File | Role |
|------|------|
| `LabelDesigner.App/Services/DpiService.cs` | DPI source of truth; initialised from window handle |
| `LabelDesigner.Core/Interfaces/IPrintService.cs` | `PixelsPerMm` property that must be set before print/PNG |
| `LabelDesigner.Core/Interfaces/IPdfExportService.cs` | `PdfExportOptions.PixelsPerMm` |
| `LabelDesigner.Infrastructure/Export/PrintService.cs` | Uses `PixelsPerMm` in scale calculation for both raster and bitmap paths |
| `LabelDesigner.Infrastructure/Export/PdfExportService.cs` | Computes `pointsPerPixel` from `PixelsPerMm`; passes as `ptp` to all draw helpers |
| `LabelDesigner.App/ViewModels/DesignerViewModel.cs` | Sets `PixelsPerMm` on services before `PrintAsync`, `PrintWithDataAsync`, `ExportPdfAsync`, `ExportPngAsync` |
| `LabelDesigner.App/ViewModels/CanvasViewport.cs` | Matrix-based screen/world mapping; uses `PixelsPerMm` for ruler labels |
