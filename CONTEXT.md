# LabelDesigner

This glossary defines the user-facing language for LabelDesigner. It exists so plan discussions and future module names distinguish the artifact the user works on from the in-memory structure the code manipulates.

## Language

**Label**:
The user-facing design artifact that is edited, previewed, printed, and saved.
_Avoid_: Scene Document, document

**Scene Document**:
The in-memory representation of one **Label**.
_Avoid_: Label, file

**Label Editing**:
The application-level behaviour that mutates a **Label** through operations like placement, copy, alignment, and ordering.
_Avoid_: Designer Commands, Scene Graph Editing

**Sheet Layout**:
The saved arrangement of one or more **Labels** on a physical page, including rows, columns, and spacing.
_Avoid_: Temporary print setting, ad hoc tiling

**Physical Sheet**:
The real output page used for print or PDF, which may contain one or more **Labels**.
_Avoid_: Canvas page, label page

**Label Stock Preset**:
A named preset that fills a **Sheet Layout** for a known physical label stock.
_Avoid_: Hard-coded sheet template, ad hoc stock setting

## Relationships

- A **Label** is represented in memory by exactly one **Scene Document**
- **Label Editing** changes a **Label** by mutating its **Scene Document**
- A **Label** may include one **Sheet Layout** that controls how it is printed or exported onto a physical page
- A **Sheet Layout** places one or more **Labels** onto a **Physical Sheet**
- A **Label Stock Preset** populates a **Sheet Layout** for a **Physical Sheet**

## Example dialogue

> **Dev:** "When a user opens a **Label**, are we loading the whole **Scene Document**?"
> **Domain expert:** "Yes — the **Label** is what the user thinks about, and the **Scene Document** is the in-memory shape we edit."
>
> **Dev:** "Where do copy and alignment behaviour live?"
> **Domain expert:** "In **Label Editing** — the interaction layer triggers it, but **Label Editing** owns the mutation rules."
>
> **Dev:** "If the same **Label** is printed several times on one sheet, where is that defined?"
> **Domain expert:** "In the **Sheet Layout** saved with the **Label**, because that arrangement is part of the intended stock geometry."
>
> **Dev:** "Does the canvas page become the whole sheet once tiling exists?"
> **Domain expert:** "No — the canvas still represents one **Label**, and the **Sheet Layout** places that **Label** onto a **Physical Sheet** at print or export time."
>
> **Dev:** "How do we support common Avery-style sheets without rebuilding the layout every time?"
> **Domain expert:** "With a **Label Stock Preset** that fills the **Sheet Layout** for that known stock."

## Flagged ambiguities

- "document" was being used for both the user-facing artifact and the in-memory structure — resolved: use **Label** for the artifact and **Scene Document** for the in-memory representation.
