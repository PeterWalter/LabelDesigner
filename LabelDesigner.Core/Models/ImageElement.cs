using LabelDesigner.Core.Enums;

namespace LabelDesigner.Core.Models;

public class ImageElement : DesignElement
{
    public string SourcePath { get; set; } = "";
    public ImageStretch Stretch { get; set; } = ImageStretch.Uniform;
}
