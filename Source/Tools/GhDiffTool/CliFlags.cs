namespace GhDiffTool;

using HopTracer.Core.Services;

public sealed class CliFlags
{
    public DiffOutputFormat Format { get; set; } = DiffOutputFormat.Text;
    public string? OutputPath { get; set; }
    public string? Commit { get; set; }
    public string? FileOldLabel { get; set; }
    public string? FileNewLabel { get; set; }
    public bool Compact { get; set; }
    public bool Verbose { get; set; }
    public bool ShowEdges { get; set; }
    public int? MaxNodes { get; set; }
    public int? MaxEdges { get; set; }
    public bool FailOnRisk { get; set; }
    public bool NoStats { get; set; }
    public bool NoDiagnostics { get; set; }
    public bool NoRisk { get; set; }
    public bool NoRisks { get; set; }
    public bool NoNodes { get; set; }

    public DiffOutputOptions ToOptions(string fileOld, string fileNew)
    {
        var isAgent = Format == DiffOutputFormat.Agent;
        return new DiffOutputOptions
        {
            FileOld = FileOldLabel ?? fileOld,
            FileNew = FileNewLabel ?? fileNew,
            CompactFormat = Compact,
            IncludeNodeDetails = Verbose && !isAgent,
            IncludeChangedEdges = ShowEdges || isAgent,
            MaxNodesToShow = MaxNodes ?? (isAgent ? 100 : 20),
            MaxEdgesToShow = MaxEdges ?? (isAgent ? 50 : 10),
            IncludeStatistics = !NoStats && !isAgent,
            IncludeDiagnostics = !NoDiagnostics,
            IncludeRiskSummary = !NoRisk,
            IncludeTopRisks = !NoRisks,
            IncludeChangedNodes = !NoNodes && !isAgent
        };
    }
}
