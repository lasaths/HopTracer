namespace HopTracer.Core.Models;

public class BaselineArtifact
{
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SnapshotPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
}

public class ForensicReportArtifact
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string FileOld { get; set; } = string.Empty;
    public string FileNew { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int AddedNodeCount { get; set; }
    public int RemovedNodeCount { get; set; }
    public int ModifiedNodeCount { get; set; }
    public int AddedEdgeCount { get; set; }
    public int RemovedEdgeCount { get; set; }
    public DiffRiskSummary RiskSummary { get; set; } = new();
    public List<NodeRiskFinding> TopRisks { get; set; } = new();
    public List<DiffDiagnostic> Diagnostics { get; set; } = new();
    public List<ForensicNodeChange> ChangedNodes { get; set; } = new();
    public List<ForensicEdgeChange> ChangedEdges { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
    public string? BaselineName { get; set; }
    public string? BaselineFingerprint { get; set; }
}

public class ForensicNodeChange
{
    public string NodeId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public List<string> Reasons { get; set; } = new();
    public int InAdded { get; set; }
    public int InRemoved { get; set; }
    public int OutAdded { get; set; }
    public int OutRemoved { get; set; }
    public List<string> Upstream { get; set; } = new();
    public List<string> Downstream { get; set; } = new();
}

public class ForensicEdgeChange
{
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string SourcePort { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string TargetPort { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;
}
