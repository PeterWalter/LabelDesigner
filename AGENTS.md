## Phase 4 — Implementation Plan

### Summary of Current State
Core, Application, Infrastructure compile. App project needs NuGet restore.
Features implemented: scene graph, undo/redo, file persistence, zoom/pan/rulers,
element types (barcode, text, shape, line, image), rotation handle, multi-select,
copy/paste, properties panel, color swatches, page templates, print/PDF/PNG export,
data binding (CSV), layers panel, print preview.

### Immediate Fixes Needed

**1. Copy/Paste broken**
- `CopySelected` stores a REFERENCE to the selected element, not a clone
- If element is deleted after copy, clipboard pointer is stale
- Paste doesn't select the pasted element, doesn't invalidate canvas
- Fix: clone at copy time, select after paste

**2. Image import broken**
- `AddImage()` enters placement mode without a file picker
- `ImageElement.SourcePath` is never populated
- Fix: show file picker -> load image -> place on canvas

**3. Element movement not fully bounded to page**
- Only initial Translate is clamped, not resize handles
- Line endpoints can be dragged outside page
- Fix: clamp all bounds modifications to page edges

**4. No Escape to cancel placement mode**
- Fix: add KeyDown handler for Escape in DesignerCanvasView

**5. No undo snapshot for property edits**
- Changing Name/Position/Size/Rotation via Properties panel bypasses undo
- Fix: record undo commands in PropertiesViewModel partial methods

**6. No line drawing tool — click-drag**
- Currently AddLine places a horizontal line of fixed length
- Should work like two-click sequence
- Fix: enter line-placement mode that draws from click-point

**7. No alignment/distribution tools**
- Align left/center/right, distribute vertically/horizontally
- Fix: add commands to DesignerViewModel

**8. No context menu (right-click)**
- Missing: Cut, Copy, Paste, Delete, Bring to Front, Send to Back
- Fix: add FlyoutBase to CanvasControl

**9. No keyboard shortcut hints**
- Ctrl+Z/Y/C/V shown only in canvas code-behind, not visible to user
- Fix: add tooltips to ribbon buttons showing shortcuts

**10. No SVG import**
- `ISvgService` interface exists but no implementation
- `Svg.Skia` NuGet not yet added to project
- Fix: Phase 5

**11. No text element double-click to edit**
- Text elements should have a double-click handler that opens inline editing
- Fix: add DoubleTapped event on canvas

### Phase 4 — Fix & Polish (immediate)

| Step | File(s) | Description |
|------|---------|-------------|
| 4.1 | DesignerViewModel.cs | Fix CopySelected: clone element at copy time |
| 4.2 | DesignerViewModel.cs | Fix PasteElement: select pasted element, RequestRedraw |
| 4.3 | DesignerViewModel.cs | Fix AddImage: show file picker, load image |
| 4.4 | DesignerViewModel.cs | Add Escape to cancel placement mode |
| 4.5 | DesignerViewModel.cs | Clamp resize handles to page bounds |
| 4.6 | DesignerViewModel.cs | Add AlignLeft/Center/Right/Top/Bottom commands |
| 4.7 | DesignerCanvasView.xaml.cs | Add DoubleTapped handler for inline editing |
| 4.8 | Rendering/RenderingService.cs | Render ImageElement from SourcePath |
| 4.9 | App.xaml.cs | Register ISvgService stub |
| 4.10 | MainWindow.xaml | Add alignment ribbon group |
| 4.11 | Build verification | dotnet build --no-restore |

### Phase 5 — Advanced (next)

| Step | File(s) | Description |
|------|---------|-------------|
| 5.1 | Infrastructure | Add Svg.Skia NuGet, implement SvgService |
| 5.2 | Core/Interface | Finalize ISvgService contract |
| 5.3 | DesignerViewModel | Add SVG file importer |
| 5.4 | Rendering | Render SVG shapes to CanvasBitmap |
| 5.5 | DesignerViewModel | Add Duplicate, BringToFront, SendToBack commands |
| 5.6 | DesignerViewModel | Context menu (right-click) handlers |
| 5.7 | MainWindow.xaml | Right-click Flyout on canvas |

### Phase 6 — Professional Polish (future)

| Step | Description |
|------|-------------|
| 6.1 | Dark theme |
| 6.2 | Recent files list |
| 6.3 | Splash screen |
| 6.4 | Tooltips on all ribbon buttons |
| 6.5 | Label stock presets (Avery, etc.) |
| 6.6 | Accessibility (Narrator support) |
| 6.7 | Unit tests for scene graph, undo/redo, persistence |
| 6.8 | Multi-language support (.resx) |
