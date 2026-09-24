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
    public DiffRiskSummary RiskSummary { get; set; } = new();
    public List<NodeRiskFinding> TopRisks { get; set; } = new();
    public List<DiffDiagnostic> Diagnostics { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
    public string? BaselineName { get; set; }
    public string? BaselineFingerprint { get; set; }
}
