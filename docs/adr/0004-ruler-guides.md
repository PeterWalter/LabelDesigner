# ADR-0004 — Ruler guides

**Status:** Accepted (May 2026)

## Context

Users need a way to align elements visually by placing horizontal and vertical reference lines on the canvas. These **Guides** should:
- Persist across save/load cycles
- Be positioned in document units (mm) for DPI-invariance
- Be visually distinct from elements (non-printing)
- Allow rapid placement by dragging from ruler edges
- Provide temporary snap feedback during element interaction

## Decision

**Guide lines are stored as a persistent collection in `SceneDocument.Guides`.**

Each `GuideLine` has:
- `Guid Id` — unique identifier for removal
- `bool IsHorizontal` — true = horizontal (fixed Y), false = vertical (fixed X)
- `double PositionMm` — position in millimeters (document units, not screen pixels)

### Placement

Guides are placed by **dragging from the ruler edge**:
- Horizontal ruler (top) → vertical guides (dragging up/down changes X position)
- Vertical ruler (left) → horizontal guides (dragging left/right changes Y position)

The `RulerControl` fires a `GuideCreated` event with the world-space position, which calls `DesignerViewModel.AddVerticalGuide(positionPx)` or `AddHorizontalGuide(positionPx)`. The ViewModel converts pixels → mm before storing.

### Rendering

`RenderService.RenderScene` renders guides **after the page but before elements**, ensuring they appear beneath all content. The rendering:
1. Converts `guide.PositionMm` → screen pixels using current `pixelsPerMm`
2. Draws a dashed line (Syncfusion `CanvasDashStyle.Dash`) in light blue (`ARGB(180, 0, 160, 240)`)
3. Scales line width by `1f / zoom` to remain crisp at all zoom levels

### Snap Feedback

During element drag, `ElementInteractionService.UpdateDrag` calls `ISnapService.Snap(...)`, which returns a list of temporary `GuideLine` objects showing which guides the element snapped to. These guides are **not persisted** — they clear after interaction ends.

### Persistence

Guides are serialized as part of `SceneDocument.Guides` in JSON:
```json
{
  "version": "2.0",
  "guides": [
    { "id": "guid-1", "isHorizontal": true, "positionMm": 50.0 },
    { "id": "guid-2", "isHorizontal": false, "positionMm": 100.0 }
  ]
}
```

On load, guides are reconstructed at their mm positions, which automatically adjust to the current window DPI via `PixelsPerMm`.

### Removal

A **"Clear All" ribbon button** calls `DesignerViewModel.ClearGuidesCommand`, which empties `Guides` and triggers a redraw. Individual guide removal (click to delete) is future work.

## Rationale

- **Position in mm, not pixels** — ensures guides remain at their intended document position when opening on a different monitor (different DPI).
- **Dashed style** — distinguishes guides from actual line elements.
- **Non-printing** — guides are stored in memory but not exported to PDF, print, or PNG, since they are editorial aids only.
- **Drag-from-ruler** — aligns with industry standard (Adobe, Figma, CorelDRAW) and reduces menu clutter.
- **Temporary snap feedback** — helps users see which guides an element is snapping to without polluting the persistent guide list.

## Alternatives considered

1. **Guides as first-class Elements** — rejected because guides are non-printing and shouldn't appear in the element list or interact with z-order.
2. **Guides stored in pixels** — rejected; leads to position drift on DPI changes.
3. **Guides created by context menu** — rejected; drag-from-ruler is faster and more discoverable.
4. **Always snap to nearest guide** — rejected; snap is optional (toggle in settings) and only applies to element edges, not all movement.

## Consequences

- Users can now place visual reference lines on the canvas by dragging from rulers.
- Guides persist across saves, aiding workflows like "label design for stock X with 1cm margins."
- The snap system now emits temporary guides to give visual feedback during drag.
- No breaking changes — guides are an additive feature in `SceneDocument` version 2.0 (and future versions).
