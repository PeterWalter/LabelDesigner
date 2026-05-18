# LabelDesigner

LabelDesigner is a WinUI-based desktop application for designing label layouts and producing barcode-driven output for print and PDF workflows.

## Current capabilities

- Scene-based label editing with selection, move/resize/rotate, zoom/pan, rulers, and layers.
- Element support: barcode, text, shape, line, and image.
- Undo/redo, copy/paste, templates, and document persistence.
- Data binding from CSV for record-driven output.
- Export and output paths for print preview, direct print flow, PDF, and PNG.

## Architecture direction

The project is being deepened around explicit seams so behavior is easier to test, evolve, and automate:

- **Interaction seam** for pointer/placement behavior.
- **Label Editing seam** for copy/paste, alignment, and element mutation flows.
- **Rendering seam** (draw-command based, see ADR) to unify preview/print/PDF fidelity.
- **Settings and layout seams** for ruler units, workspace layout, and output pagination.

Domain vocabulary is documented in `CONTEXT.md`, and rendering decisions are tracked in `docs/adr/0001-draw-command-rendering-seam.md`.

## Planned roadmap highlights

- Workspace docking for Layers/Properties tool panes with persisted layout and reset.
- Ruler unit settings (mm/cm/in) with page origin aligned to ruler `0,0`.
- Snap-to-grid and alignment guides with clearer visual feedback.
- Richer text formatting (font family/size/style, multiline, alignment, spacing).
- Unified print/PDF pagination with single-record-per-page and tiled multi-label merge modes.
- Accessibility and keyboard-first shell feedback improvements.

## Repository layout

- `LabelDesigner.App` - WinUI shell, views, and viewmodels.
- `LabelDesigner.Application` - application-level orchestration and use-case modules.
- `LabelDesigner.Core` - domain models, value objects, and interfaces.
- `LabelDesigner.Infrastructure` - rendering, export, persistence, and external integrations.
- `LabelDesigner.Tests` - test project (to be expanded as seams are deepened).
