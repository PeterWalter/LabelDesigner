using LabelDesigner.Core.Models;
using LabelDesigner.Core.ValueObjects;

namespace LabelDesigner.Core.Interfaces;

public interface ISnapService
{
    double GridSize { get; set; }
    double Threshold { get; set; }

    RectD Snap(RectD moving, IEnumerable<RectD> others, double pixelsPerMm, out IReadOnlyList<GuideLine> guides);
}
