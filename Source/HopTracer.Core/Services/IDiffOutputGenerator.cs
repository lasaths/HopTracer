using HopTracer.Core.Models;

namespace HopTracer.Core.Services;

public class DiffOutputOptions
{
    public string? FileOld { get; set; }
    public string? FileNew { get; set; }
    public bool IncludeStatistics { get; set; } = true;
    public bool IncludeDiagnostics { get; set; } = true;
    public bool IncludeRiskSummary { get; set; } = true;
    public bool IncludeTopRisks { get; set; } = true;
    public bool IncludeChangedNodes { get; set; } = true;
    public bool IncludeChangedEdges { get; set; } = false;
    public bool IncludeNodeDetails { get; set; } = false;
    public int MaxNodesToShow { get; set; } = 20;
    public int MaxEdgesToShow { get; set; } = 10;
    public bool CompactFormat { get; set; } = false;
}

public enum DiffOutputFormat
{
    Text,
    Markdown,
    Json,
    Html,
    Agent
}
