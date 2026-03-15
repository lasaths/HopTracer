namespace HopTracer.Core.Models;

/// <summary>
/// Represents a Grasshopper graph containing nodes and edges.
/// Equivalent to Python's Graph dataclass.
/// </summary>
public class Graph
{
    public Dictionary<string, Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public GraphMetadata Metadata { get; set; } = new();
}

public class GraphMetadata
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime? Date { get; set; }
    public string Author { get; set; } = "";
    public string FileVersion { get; set; } = "";
    public string ArchiveVersion { get; set; } = "";
    public int UnresolvedEdgeReferences { get; set; }
    public int ClusterPreviewParsed { get; set; }
    public int ClusterPreviewFailed { get; set; }
    public int ClusterPreviewDepthLimitHits { get; set; }
    public List<string> Diagnostics { get; set; } = new();
}
