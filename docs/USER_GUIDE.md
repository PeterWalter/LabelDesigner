# LabelDesigner — User Guide

> **Version:** May 2026  
> LabelDesigner is a Windows desktop application for designing, saving, and printing labels. Labels can contain barcodes, text, shapes, lines, and images. Finished labels can be printed directly, exported to PDF or PNG, or merged with a CSV data source to generate one label per row.

---

## Contents

1. [Interface overview](#1-interface-overview)
2. [Creating a label](#2-creating-a-label)
3. [Working with elements](#3-working-with-elements)
   - 3.1 Barcode
   - 3.2 Text
   - 3.3 Shape
   - 3.4 Line
   - 3.5 Image
4. [Selecting and editing elements](#4-selecting-and-editing-elements)
5. [Guides](#5-guides)
6. [Layers](#6-layers)
7. [Page settings](#7-page-settings)
8. [Saving and opening files](#8-saving-and-opening-files)
9. [Templates](#9-templates)
10. [Data merge (CSV)](#10-data-merge-csv)
11. [Print and export](#11-print-and-export)
12. [Settings](#12-settings)
13. [Keyboard shortcuts](#13-keyboard-shortcuts)

---

## 1. Interface overview

```
┌─────────────────────────────────────────────────────────────────────┐
│  Ribbon (File / Home / Barcode / Text)                              │
├───────────┬───────────────────────────────────────────┬─────────────┤
│           │                                           │             │
│  Layers   │              Canvas                       │ Properties  │
│  panel    │         (label design area)               │   panel     │
│           │                                           │             │
├───────────┴───────────────────────────────────────────┴─────────────┤
│  Status bar  (zoom · cursor mm · element count · snap)              │
└─────────────────────────────────────────────────────────────────────┘
```

| Area | Purpose |
|---|---|
| **Ribbon — File tab** | New, Open, Save, Save As, Save Template, New from Template, Recent files, Settings |
| **Ribbon — Home tab** | Insert elements, clipboard, arrange, align, page size, zoom, print/export, data merge |
| **Ribbon — Barcode tab** *(context)* | Appears when a barcode is selected: symbology, text position, data merge |
| **Ribbon — Text tab** *(context)* | Appears when a text element is selected: font, size, style, alignment |
| **Canvas** | White page on grey background; rulers along top and left edges |
| **Layers panel** (left) | Add/delete/hide/lock layers; shows elements per layer |
| **Properties panel** (right) | Edit position, size, rotation, and element-specific properties |
| **Status bar** | Current zoom level, cursor position in mm, element count, snap indicator |

### Rulers and units
Rulers read from the **top-left corner of the page**, which is always 0 mm. Units (mm, cm, or inches) are configured in **Settings**.

### Zoom and pan
- **Scroll wheel** — zoom in/out at the cursor position.
- **Right-drag** (or middle-drag) — pan the canvas.
- **Ctrl +** / **Ctrl −** — zoom in/out.
- **Ctrl 0** — fit the whole page in the window.

---

## 2. Creating a label

### Starting a new label
1. Click **File → New** (or press **Ctrl+N**).
2. The canvas resets to a blank page. The title bar shows `Untitled — LabelDesigner`.

### Setting the page size
Use the **Page** group on the **Home** tab:

| Button | Size |
|---|---|
| **A4** | 210 × 297 mm |
| **A5** | 148 × 210 mm |
| **A3** | 297 × 420 mm |
| **4×5** | 4 × 5 inches (label stock) |
| **6×4** | 6 × 4 inches (label stock) |
| **8×3** | 8 × 3 inches (label stock) |
| **Flip** | Toggle portrait ↔ landscape |

For a custom size, type exact values in the **Width (mm)** and **Height (mm)** boxes in the Properties panel when no element is selected.

---

## 3. Working with elements

### Placing elements

All element buttons are in the **Insert** group on the **Home** tab. Click the button — the cursor changes to indicate placement mode. Then **click on the canvas** to drop the element at that position.

Press **Escape** at any time to cancel placement mode without adding an element.

---

### 3.1 Barcode

Click **Barcode** (or press **B**) → click the canvas.

**Barcode properties (Properties panel):**

| Property | Description |
|---|---|
| Value | The data encoded in the barcode. Use `{ColumnName}` for data merge. |
| Symbology | Code 128, Code 39, QR Code, EAN-13, EAN-8, UPC-A, DataMatrix, PDF417, Aztec, ITF |
| Text position | Where to display the human-readable value: Top, Bottom, Left, Right, or Hidden |
| Text font / size | Font family and size for the printed text beneath/beside the bars |
| **B** / *I* | Bold / italic for the barcode text label |
| Text color | Color of the printed text |

**Tip — hide the text:** Set **Text position** to *Hidden* (or *None*) to display the barcode bars only.

**Changing symbology from the ribbon:** Select the barcode element → the **Barcode** context tab appears in the ribbon → choose a symbology from the **Barcode Type** dropdown.

---

### 3.2 Text

Click **Text** (or press **T**) → click the canvas.

**Text properties (Properties panel and context tab):**

| Property | Description |
|---|---|
| Text | The displayed string. Use `{ColumnName}` for data merge. |
| Font family | Any system font |
| Font size | In points |
| Bold / Italic / Underline | Style toggles |
| Alignment | Left, Center, Right |
| Multiline | Allow line breaks inside the element |
| Line spacing | Multiplier for line height |
| Color | Text foreground color |

---

### 3.3 Shape

Click **Shape** → click the canvas. A rectangle is placed.

**Shape properties:**

| Property | Description |
|---|---|
| Fill color | Interior fill (transparent = no fill) |
| Stroke color | Border color |
| Stroke width | Border thickness in mm |
| Corner radius | Rounds the corners (0 = sharp) |

---

### 3.4 Line

Click **Line** → click the canvas.

**Line properties:**

| Property | Description |
|---|---|
| Stroke color | Line color |
| Stroke width | Thickness in mm |
| Rotation | Angle in degrees |

Resize handles on the endpoints let you drag the line to any length and angle.

---

### 3.5 Image

Click **Image** → a file picker opens → select a PNG, JPEG, BMP, or GIF file → click the canvas to place it.

Drag the resize handles to scale the image. The image is embedded in the saved label file.

---

## 4. Selecting and editing elements

### Selecting
- **Click** an element to select it. The Properties panel shows its settings.
- **Shift+Click** or **drag a selection rectangle** to select multiple elements.
- **Ctrl+A** — select all elements on the canvas.
- **Click an empty area** to deselect.

### Moving
Drag a selected element to move it. Hold **Shift** while dragging to constrain movement to horizontal or vertical.

Arrow keys (**←↑→↓**) nudge the selected element 1 mm at a time (configurable snap increment).

### Resizing
Drag any of the eight white square handles around the selection box. Hold **Shift** to resize proportionally.

### Rotating
Drag the **circular handle** above the selection box to rotate freely. Or type an exact angle in the **Rotation** field in the Properties panel.

### Alignment (multiple elements)
Select two or more elements (Shift+Click), then use the **Align** group on the **Home** tab:

| Button | Action |
|---|---|
| Align Left | Aligns left edges to the leftmost element |
| Center H | Centers horizontally |
| Align Right | Aligns right edges to the rightmost element |
| Align Top | Aligns top edges to the topmost element |
| Align Bot | Aligns bottom edges to the bottommost element |
| Dist H | Spreads elements evenly — horizontal |
| Dist V | Spreads elements evenly — vertical |

### Copy / Paste
- **Ctrl+C** — copy the selected element (a snapshot is taken at copy time).
- **Ctrl+V** — paste. The copy is placed slightly offset from the original.
- **Delete** — delete selected element(s).

### Undo / Redo
- **Ctrl+Z** — undo the last action.
- **Ctrl+Y** — redo.

Every element property change, move, resize, rotation, and delete is undoable.

---

## 5. Guides

**Guides** are non-printing reference lines that help you align elements on the canvas. Guides can be horizontal or vertical, and they persist when you save the label.

### Creating a guide

1. **Click and drag from the ruler edge** (top ruler for vertical guides, left ruler for horizontal guides).
2. A dashed blue line follows your cursor, showing the preview position.
3. **Release the mouse** to place the guide at that position.

Guides are positioned in document units (mm, cm, or inches — same as the ruler units in Settings).

### Clearing guides

1. Click **Home → Guides group → Clear All**.
2. All guides are removed from the canvas.

### Why use guides?

- Align elements at precise positions without snapping them to a grid.
- Mark safe zones (e.g., 1 cm from each edge) to avoid printer margins.
- Plan multi-label layouts before adding elements.

Guides do **not** print or export to PDF / PNG — they are editorial aids only.

---

## 6. Layers

The **Layers panel** (left side) organises elements into named groups.

| Action | How |
|---|---|
| Add layer | Click **+** at the top of the Layers panel |
| Delete layer | Select the layer row → click **−** |
| Rename layer | Double-click the layer name |
| Hide layer | Click the **eye** icon — hidden layers do not appear on canvas or in exports |
| Lock layer | Click the **padlock** — locked layers cannot be selected or moved |
| Move element to a layer | Drag the element row in the Layers panel |
| Reorder layers | Drag the layer row; elements in higher layers appear in front |

Elements on higher layers always render in front of elements on lower layers. Within a layer, later-added elements render in front.

---

## 7. Page settings

When **no element is selected**, the Properties panel shows page settings:

| Setting | Description |
|---|---|
| Width (mm) | Label width |
| Height (mm) | Label height |
| Orientation | Portrait / Landscape |
| Margins (T/R/B/L) | Margin guides drawn on the canvas (dotted) |
| Background color | Canvas page fill color |
| Sheet layout | Rows and columns for multi-label printing (see Section 10) |

---

## 8. Saving and opening files

### Save
- **Ctrl+S** — save to the current file. If the label has not been saved before, a Save dialog opens.
- **File → Save As** — save to a new location.

Labels are saved as **`.ldlabel`** files (JSON format).

### Open
- **File → Open** (Ctrl+O) — opens a file picker filtered to `.ldlabel` files.
- **File → Recent** — shows recently opened files in a dropdown. Select a file name and click **Open Recent**.

### Title bar
The window title shows the current state:

| Title | Meaning |
|---|---|
| `Untitled — LabelDesigner` | New, unsaved label |
| `Untitled* — LabelDesigner` | New label with unsaved changes |
| `MyLabel — LabelDesigner` | Saved label, no changes |
| `MyLabel* — LabelDesigner` | Saved label with unsaved changes |

---

## 9. Templates

A **template** is a reusable label layout (`.ldtemplate`) that you can open as a starting point for new labels without modifying the original.

### Saving a template
1. Design your label.
2. Click **File → Save Template**.
3. Choose a location and file name (`.ldtemplate` extension).
4. Click **Save**.

> The current document path is **not** changed by saving a template. Ctrl+S after saving a template still saves (or prompts to save) your working label — not the template.

### Starting a new label from a template
1. Click **File → New from Template**.
2. Select a `.ldtemplate` file.
3. The template content loads on the canvas as a new **untitled** label.
4. Use **Ctrl+S** or **File → Save As** to save your work as a new `.ldlabel` file.

### Typical template workflow

```
Design layout → Save Template (MyTemplate.ldtemplate)
   ↓
New from Template → makes "Untitled*"
   ↓
Fill in barcodes / text values
   ↓
Save As → MyOrder-2026-05.ldlabel
```

---

## 10. Data merge (CSV)

Data merge prints multiple versions of the same label, each using one row from a CSV file.

### Setup

1. **Home → Select CSV** — choose a comma-separated file. Column headers become field names.
2. **Home → Merge Panel** — opens the Data Merge panel which shows all columns and a row preview.

### Binding a field to an element

**Text elements:** Type `{ColumnName}` directly in the **Text** property field. The matching CSV column is substituted on each printed label.

**Barcode elements:** Select the barcode → go to the **Barcode** context tab → click **Bind Column**. This inserts a `{ColumnName}` binding into the barcode value.

The Data Merge panel preview shows what the label looks like for the currently selected row.

### Merge modes

| Mode | Description |
|---|---|
| **1/Page** | Each CSV row produces one page in the output |
| **Multi/Page** | Rows are tiled on each page using the Sheet Layout (rows × columns) |

Switch modes using the **1/Page** and **Multi/Page** buttons in **Home → Data** group, or use the **Data Merge panel**.

### Sheet layout for tiling

Open page settings (click an empty area of the canvas → Properties panel):

| Setting | Description |
|---|---|
| Rows | Number of label rows per sheet |
| Columns | Number of label columns per sheet |
| H gap (mm) | Horizontal gap between labels |
| V gap (mm) | Vertical gap between labels |

### Printing merged labels

- **Home → Print All** — prints all CSV rows using the current merge mode.
- **Home → PDF** / **PNG** when a CSV is loaded also exports all rows.

---

## 11. Print and export

| Action | Where | Description |
|---|---|---|
| **Preview** | Home → Print & Export | Opens a print preview window showing the full-resolution output |
| **Print** | Home → Print & Export | Opens the Windows print dialog |
| **PDF** | Home → Print & Export | Saves a PDF file (file picker appears) |
| **PNG** | Home → Print & Export | Saves a PNG image at screen resolution (file picker appears) |
| **Print All** | Home → Data | Prints all CSV rows using the merge mode |

### Notes on export quality
- PDF and PNG render at the label's actual DPI — output is sharp on any monitor resolution.
- If your label contains a barcode, the barcode bars are vector-accurate in PDF.
- PNG export creates one file per printed page. When using **Multi/Page** merge, each output file is named with a page number suffix.

---

## 12. Settings

Open **File → Settings** (or click **Settings** in the Application group of the File tab).

| Setting | Description |
|---|---|
| **Theme** | Light / Dark / System default |
| **Units** | Millimetres, centimetres, or inches |
| **Snap to grid** | Enable/disable element snapping |
| **Grid size** | Snap grid increment in mm |
| **Default font** | Font applied to new text elements |
| **Default barcode type** | Symbology used when a new barcode is inserted |

Settings are saved automatically and restored when the application restarts.

---

## 13. Keyboard shortcuts

### File
| Shortcut | Action |
|---|---|
| Ctrl+N | New label |
| Ctrl+O | Open label |
| Ctrl+S | Save |
| Ctrl+Shift+S | Save As |

### Edit
| Shortcut | Action |
|---|---|
| Ctrl+Z | Undo |
| Ctrl+Y | Redo |
| Ctrl+C | Copy selected element |
| Ctrl+V | Paste |
| Delete | Delete selected element(s) |
| Ctrl+A | Select all |

### Insert
| Shortcut | Action |
|---|---|
| B | Activate Barcode placement tool |
| T | Activate Text placement tool |
| L | Activate Line placement tool |
| S | Switch to Select tool |
| Escape | Cancel placement / deselect |

### View
| Shortcut | Action |
|---|---|
| Ctrl++ | Zoom in |
| Ctrl+− | Zoom out |
| Ctrl+0 | Zoom to fit |
| Mouse wheel | Zoom at cursor |
| Right-drag | Pan canvas |

### Arrange
| Shortcut | Action |
|---|---|
| Ctrl+G | Group selected elements |
| Ctrl+Shift+G | Ungroup |
| ←↑→↓ | Nudge selected element 1 mm |

---

*For technical and architectural details see [CONTEXT.md](../CONTEXT.md) and the [ADR documents](adr/).*
