using LabelDesigner.Core.Models;
using LabelDesigner.Core.Rendering;

namespace LabelDesigner.Core.Interfaces;

/// <summary>
/// Converts a <see cref="SceneDocument"/> into a flat, ordered list of
/// platform-agnostic <see cref="DrawCommand"/>s. Consumers (Win2D canvas,
/// PDF export, PNG rasterizer, unit tests) translate these commands into
/// their native drawing calls without depending on Windows-specific APIs.
/// </summary>
public interface ISceneCommandRenderer
{
    /// <summary>
    /// Renders <paramref name="document"/> and returns the draw commands in
    /// paint order (back-to-front). Hidden layers produce no commands.
    /// </summary>
    IReadOnlyList<DrawCommand> Render(SceneDocument document);
}
