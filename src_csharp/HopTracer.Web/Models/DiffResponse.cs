using HopTracer.Core.Models;

namespace HopTracer.Web.Models;

public class DiffResponse
{
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public DiffMeta Meta { get; set; } = new();
    public GraphMetadata? OldMeta { get; set; }
    public GraphMetadata? NewMeta { get; set; }
}

public class DiffMeta
{
    public string GeneratedAt { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
}
