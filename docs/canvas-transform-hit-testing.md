# Canvas Transforms, Screen↔World Mapping, DPI, Zoom, and Hit Testing

This document describes how the designer canvas maps pointer input to world coordinates, renders with zoom/pan, and resolves selection/hit testing.

## 1. Coordinate spaces

The runtime uses three coordinate spaces:

1. **Screen space**  
   Coordinates from WinUI pointer events (`PointerPressed`, `PointerMoved`, wheel, double tap).  
   Unit: device-independent pixels (DIPs) from UI event system.

2. **World space**  
   Canvas/page coordinates used by scene graph and interaction logic.  
   In this codebase, world dimensions are expressed in pixels derived from page mm × `PixelsPerMm`.

3. **Element-local space**  
   Per-element coordinates after applying inverse of element transform (rotation/scale around center).

## 2. DPI source and world unit scale

`DpiService` computes `PixelsPerMm` from OS DPI:

- `PixelsPerMm = GetDpiForWindow(hwnd) / 25.4`
- fallback path: `GetDpiForSystem() / 25.4`

This value is consumed in `DesignerViewModel` and rendering paths:

- Page pixel width: `Page.WidthMm * PixelsPerMm`
- Page pixel height: `Page.HeightMm * PixelsPerMm`

So page size is authored in millimeters, but interaction/rendering use pixel-based world coordinates after conversion.

## 3. Viewport transform (world → screen)

`CanvasViewport` defines the camera-like transform:

```text
M_viewport = Translate(-OffsetX, -OffsetY) * Scale(Zoom)
screen = world * M_viewport
```

Implementation parity:

- `CanvasViewport.GetViewportTransform()`
- `RenderService.RenderScene(...)` sets `DrawingSession.Transform` with the same matrix order

This parity is critical: drawing and pointer conversion must use mathematically identical transforms.

## 4. Inverse transform (screen → world)

Pointer coordinates are converted using the inverse viewport matrix:

```text
world = screen * inverse(M_viewport)
```

`CanvasViewport.ScreenToWorld(...)` calls `Matrix3x2.Invert(...)` and transforms the pointer point with the inverse.

All selection, drag, resize, rotate, marquee, hover, and inline edit hit checks operate on this world point.

## 5. Zoom-to-fit and panning

### Zoom-to-fit

`CanvasViewport.ZoomToFit(...)` computes:

- `fitX = (canvasWidth - 2*margin) / pageWidth`
- `fitY = (canvasHeight - 2*margin) / pageHeight`
- `Zoom = min(fitX, fitY)`

Offsets center the page with current zoom:

- `OffsetX = (pageWidth*Zoom - canvasWidth) / 2`
- `OffsetY = (pageHeight*Zoom - canvasHeight) / 2`

### Mouse wheel behavior

In `DesignerCanvasView.OnPointerWheelChanged(...)`:

- **Ctrl + wheel**: zoom around cursor anchor
  1. compute `worldBefore = ScreenToWorld(cursor)`
  2. apply zoom step/clamp
  3. compute `worldAfter = ScreenToWorld(cursor)`
  4. adjust offsets by delta so same world point stays under cursor
- **Shift + wheel**: horizontal pan (`OffsetX`)
- **Wheel only**: vertical pan (`OffsetY`)

## 6. Render pipeline transform composition

Render order inside `RenderService`:

1. Set viewport transform on drawing session (`M_viewport`).
2. For each visible element:
   - compute element local transform `M_element`
   - apply `ds.Transform = M_element * currentTransform`
   - draw element geometry
   - restore previous transform

Element transform in `DesignElement.GetLocalTransform()`:

```text
M_element =
  Translate(-center) *
  Scale(ScaleX, ScaleY) *
  Rotate(RotationRadians) *
  Translate(+center)
```

This gives center-pivot scaling/rotation.

## 7. Pointer → selection flow

High-level flow:

1. Canvas event receives screen point.
2. Convert screen → world via `Viewport.ScreenToWorld`.
3. `DesignerViewModel.PointerPressed/PointerMoved` calls scene hit-test with world point.
4. Scene graph resolves top-most hittable element.
5. Selection state updates (`Selected`, `SelectedIds`) and redraw request emitted.

Core entry points:

- `DesignerCanvasView.OnPointerPressed/OnPointerMoved`
- `DesignerViewModel.PointerPressed/PointerMoved/SelectElementAt`
- `SceneGraphService.HitTest(...)`

## 8. Scene hit testing order and filtering

`SceneGraphService.HitTest(PointD p)`:

1. Iterate layers from top to bottom (`Layers.Count - 1` down to `0`).
2. Skip invisible or locked layers.
3. Iterate layer elements by descending `ZIndex`.
4. Skip invisible or locked elements.
5. Return first element where `el.HitTest(worldPoint)` is true.

This yields expected painter-order selection (frontmost wins).

`HitTestAll(...)` uses same filtering/order but returns all matches.

## 9. Element-level hit testing with transforms

`DesignElement.HitTest(worldPoint)`:

1. Convert world point to local object space:
   - `local = worldPoint * InverseTransform`
2. Axis-aligned bounds check against local `Bounds`.

`InverseTransform` is inverse of `GetLocalTransform()`.  
Result: hit tests stay aligned with rotated/scaled elements because point is untransformed into element local space before bounds check.

## 10. Selection handles and hover handles

Two related but distinct systems:

1. **Render-time handles** (`RenderService.DrawSelectionHandles`)  
   Visual corners/edges/rotation handle, scaled by zoom factor for consistent apparent size.

2. **Interaction-time handles** (`ElementInteractionService.GetHoverHandle`)  
   Detects corner/edge/inside/rotation zones in world space. Rotation handle offset scales with zoom:
   - `rotationOffset = 20 / max(zoom, 0.25)`

Detected handle drives interaction mode: move, resize, rotate.

## 11. Drag/resize/rotate updates and page bounds

`ElementInteractionService.UpdateDrag(...)`:

- Move: translate by pointer delta, optional snap, clamp to page rect
- Resize: resize from active handle, min-size enforcement, optional snap-to-grid, clamp
- Rotate: angle delta around element center

Page clamp rectangle is produced in `DesignerViewModel.GetPageRect()` using page mm × `PixelsPerMm`.

## 12. Marquee selection

Marquee rectangle is built in world space (`BeginMarqueeSelection`, `UpdateMarqueeSelection`) and finalized with:

- `SceneGraphService.GetElementsInRect(rect)`

Current behavior uses axis-aligned bounds overlap test (`el.Bounds`) and does not perform polygon-accurate rotated-shape intersection.

## 13. Why this architecture is stable

The key design decision is **matrix parity**:

- Render uses `M_viewport`
- Input uses `inverse(M_viewport)`

When those stay paired, zoom/pan/DPI changes do not drift hit testing from visuals.

Element hit testing also uses transform inverse, so object rotation/scale stays consistent between drawing and selection.

