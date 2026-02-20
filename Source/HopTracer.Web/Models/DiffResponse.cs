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
    public string? FileOld { get; set; }
    public string? FileNew { get; set; }
    public string? CommitHash { get; set; }
    public string? CommitAuthor { get; set; }
    public string? CommitDate { get; set; }
    public string? CommitMessage { get; set; }
    public string? SourcePathOld { get; set; }
    public string? SourcePathNew { get; set; }
    public string? SourceTypeOld { get; set; }
    public string? SourceTypeNew { get; set; }
    public string? SourceHashOld { get; set; }
    public string? SourceHashNew { get; set; }
    public List<DiffDiagnostic> Diagnostics { get; set; } = new();
    public List<NodeRiskFinding> TopRisks { get; set; } = new();
    public DiffRiskSummary RiskSummary { get; set; } = new();
    public List<DiffStageTiming> StageTimings { get; set; } = new();
    public string? BaselineName { get; set; }
    public bool? BaselinePassed { get; set; }
}
