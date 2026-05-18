namespace LabelDesigner.Core.Rendering;

/// <summary>
/// Platform-agnostic draw command emitted by the scene renderer.
/// Consumers (canvas, print, PDF, tests) interpret these without needing Win2D.
/// </summary>
public abstract record DrawCommand;

/// <param name="Text">Text content to render.</param>
/// <param name="X">Left edge in document units.</param>
/// <param name="Y">Top edge in document units.</param>
/// <param name="Width">Available width; 0 means unbounded.</param>
/// <param name="Height">Available height; 0 means unbounded.</param>
/// <param name="Color">Fill color as #RRGGBB or #AARRGGBB.</param>
/// <param name="FontFamily">Font family name.</param>
/// <param name="FontSize">Font size in points.</param>
/// <param name="Bold">Bold weight.</param>
/// <param name="Italic">Italic style.</param>
public record DrawTextCommand(
    string Text,
    double X, double Y, double Width, double Height,
    string Color,
    string FontFamily,
    double FontSize,
    bool Bold,
    bool Italic) : DrawCommand;

/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <param name="Fill">Fill color string, empty = no fill.</param>
/// <param name="Stroke">Stroke color string, empty = no stroke.</param>
/// <param name="StrokeWidth">Stroke width in document units.</param>
public record DrawRectCommand(
    double X, double Y, double Width, double Height,
    string Fill, string Stroke, double StrokeWidth) : DrawCommand;

/// <param name="CX">Center X.</param>
/// <param name="CY">Center Y.</param>
/// <param name="RadiusX">Horizontal radius.</param>
/// <param name="RadiusY">Vertical radius.</param>
/// <param name="Fill">Fill color string.</param>
/// <param name="Stroke">Stroke color string.</param>
/// <param name="StrokeWidth">Stroke width.</param>
public record DrawEllipseCommand(
    double CX, double CY, double RadiusX, double RadiusY,
    string Fill, string Stroke, double StrokeWidth) : DrawCommand;

/// <param name="X1">Start X.</param>
/// <param name="Y1">Start Y.</param>
/// <param name="X2">End X.</param>
/// <param name="Y2">End Y.</param>
/// <param name="Color">Line color string.</param>
/// <param name="StrokeWidth">Line width.</param>
public record DrawLineCommand(
    double X1, double Y1, double X2, double Y2,
    string Color, double StrokeWidth) : DrawCommand;

/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
/// <param name="SourcePath">File path or URI of the image.</param>
public record DrawImageCommand(
    double X, double Y, double Width, double Height,
    string SourcePath) : DrawCommand;

/// <param name="Points">Polygon vertices.</param>
/// <param name="Fill">Fill color string.</param>
/// <param name="Stroke">Stroke color string.</param>
/// <param name="StrokeWidth">Stroke width.</param>
public record DrawPolygonCommand(
    IReadOnlyList<(double X, double Y)> Points,
    string Fill, string Stroke, double StrokeWidth) : DrawCommand;
