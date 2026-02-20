namespace HopTracer.Core.Models;

/// <summary>
/// Represents a node (component) in a Grasshopper graph.
/// Equivalent to Python's Node dataclass.
/// </summary>
public class Node
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public double X { get; set; } = 0.0;
    public double Y { get; set; } = 0.0;
    public double W { get; set; } = 0.0;
    public double H { get; set; } = 0.0;
    public string Status { get; set; } = "same"; // same, added, removed, modified
    public double Dx { get; set; } = 0.0;
    public double Dy { get; set; } = 0.0;
    public int InAdded { get; set; } = 0;
    public int InRemoved { get; set; } = 0;
    public int OutAdded { get; set; } = 0;
    public int OutRemoved { get; set; } = 0;

    /// <summary>
    /// Inputs and outputs with port-level metadata.
    /// </summary>
    public List<Port> Inputs { get; set; } = new();
    public List<Port> Outputs { get; set; } = new();

    /// <summary>
    /// Freeform property map for extra details (e.g., component metadata).
    /// </summary>
    public Dictionary<string, string> Properties { get; set; } = new();
    
    /// <summary>
    /// Old property values for modified nodes (used for comparison).
    /// </summary>
    public Dictionary<string, string>? PropertiesOld { get; set; }

    /// <summary>
    /// Heuristic impact score for review prioritization (0-100).
    /// </summary>
    public int RiskScore { get; set; } = 0;

    /// <summary>
    /// Human-readable reasons that contributed to the risk score.
    /// </summary>
    public List<string> RiskReasons { get; set; } = new();
}

public class Port
{
    public string Id { get; set; } = string.Empty; // Stable identifier (e.g., param GUID or synthetic)
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Kind { get; set; } = ""; // input/output
    public string Type { get; set; } = "";
    public string? Value { get; set; }
    public string Status { get; set; } = "same"; // same, added, removed, modified
    public bool ValueChanged { get; set; } = false;
    public string? ValueOld { get; set; }
    public string? ValueNew { get; set; }
}
