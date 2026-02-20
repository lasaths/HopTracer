namespace HopTracer.Core.Models;

public class DiffComputation
{
    public List<Node> Nodes { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public List<DiffDiagnostic> Diagnostics { get; set; } = new();
    public List<NodeRiskFinding> TopRisks { get; set; } = new();
    public DiffRiskSummary RiskSummary { get; set; } = new();
}

public class DiffDiagnostic
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "info"; // info, warning, error
    public string Message { get; set; } = string.Empty;
}

public class NodeRiskFinding
{
    public string NodeId { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string NodeStatus { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public List<string> Reasons { get; set; } = new();
}

public class DiffRiskSummary
{
    public int CriticalCount { get; set; } // >=80
    public int HighCount { get; set; }     // 60-79
    public int MediumCount { get; set; }   // 30-59
    public int LowCount { get; set; }      // 1-29
}

public class DiffStageTiming
{
    public string Name { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}
