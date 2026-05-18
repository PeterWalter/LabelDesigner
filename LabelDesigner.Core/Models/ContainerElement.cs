namespace LabelDesigner.Core.Models;

public class ContainerElement : DesignElement
{
    public List<Guid> ChildIds { get; set; } = new();
}
