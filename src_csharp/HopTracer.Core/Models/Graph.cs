namespace HopTracer.Core.Models;

/// <summary>
/// Represents a Grasshopper graph containing nodes and edges.
/// Equivalent to Python's Graph dataclass.
/// </summary>
public class Graph
{
    public Dictionary<string, Node> Nodes { get; set; } = new();
    public List<(string Source, string Target)> Edges { get; set; } = new();
}
