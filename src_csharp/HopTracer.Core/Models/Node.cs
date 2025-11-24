namespace HopTracer.Core.Models;

/// <summary>
/// Represents a node (component) in a Grasshopper graph.
/// Equivalent to Python's Node dataclass.
/// </summary>
public class Node
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public double X { get; set; } = 0.0;
    public double Y { get; set; } = 0.0;
    public double W { get; set; } = 0.0;
    public double H { get; set; } = 0.0;
    public string Status { get; set; } = "same"; // same, added, removed, modified
    public double Dx { get; set; } = 0.0;
    public double Dy { get; set; } = 0.0;
    public int InAdded { get; set; } = 0;
    public int InRemoved { get; set; } = 0;
    public int OutAdded { get; set; } = 0;
    public int OutRemoved { get; set; } = 0;
}
