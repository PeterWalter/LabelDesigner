# Use a draw-command seam for rendering

LabelDesigner will introduce a draw-command seam for rendering instead of keeping Win2D types at the interface. We chose this because rendering depth depends on the interface being testable, and a draw-command seam lets preview, print, and PDF adapters share one module contract without forcing tests to stand up Win2D.
