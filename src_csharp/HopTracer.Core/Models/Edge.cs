namespace HopTracer.Core.Models;

/// <summary>
/// Represents an edge (connection) between two nodes in a Grasshopper graph.
/// Equivalent to Python's Edge dataclass.
/// </summary>
public class Edge
{
    public string Source { get; set; } = string.Empty;
    public string SourcePort { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string TargetPort { get; set; } = string.Empty;
    public string Status { get; set; } = "same"; // same, added, removed
}
