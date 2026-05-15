namespace LabelDesigner.Core.Models;

public class SceneDocument
{
    public string Version { get; set; } = "1.0";
    public PageNode Page { get; set; } = new();
    public List<LayerNode> Layers { get; } = new();
    public List<DesignElement> AllElements { get; } = new();
    public DataSourceConfig? DataSource { get; set; }
}

public class PageNode
{
    public double WidthMm { get; set; } = 100;
    public double HeightMm { get; set; } = 50;
    public double Dpi { get; set; } = 300;
    public Margins Margins { get; set; } = new();
}

public class LayerNode
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "Layer";
    public bool Visible { get; set; } = true;
    public bool Locked { get; set; }
    public List<Guid> ElementIds { get; } = new();
}

public record Margins(double Left = 2, double Top = 2, double Right = 2, double Bottom = 2);

public class DataSourceConfig
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
}
