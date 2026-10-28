using HopTracer.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HopTracer.Core.Services;

public static class DiffAgentFormatter
{
    private const int TextPropertyMaxLength = 4000;

    public static string Generate(DiffComputation diff, DiffOutputOptions options)
    {
        var resolver = new DiffGraphResolver(diff.Nodes);
        var stats = BuildStatistics(diff);
        var totalChangedNodes = diff.Nodes.Count(n => n.Status != "same");
        var totalChangedEdges = diff.Edges.Count(e => e.Status != "same");
        var changedNodes = GetChangedNodes(diff, options.MaxNodesToShow);
        var wireChanges = GetWireChanges(diff, resolver, options.MaxEdgesToShow);
        var (propertyChanges, opaqueChanges) = GetPropertyChanges(changedNodes);
        var nodeChanges = BuildNodeChanges(diff, changedNodes, wireChanges, resolver);

        var result = new AgentDiffReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            FileOld = options.FileOld,
            FileNew = options.FileNew,
            Summary = BuildSummary(stats, diff.RiskSummary, wireChanges, propertyChanges, opaqueChanges),
            Statistics = stats,
            RiskSummary = diff.RiskSummary,
            TopRisks = diff.TopRisks,
            WireChanges = wireChanges,
            PropertyChanges = propertyChanges,
            OpaqueChanges = opaqueChanges,
            NodeChanges = nodeChanges,
            Diagnostics = diff.Diagnostics,
            TotalChangedNodes = totalChangedNodes,
            TotalChangedEdges = totalChangedEdges,
            TruncatedNodes = totalChangedNodes > changedNodes.Count,
            TruncatedEdges = totalChangedEdges > wireChanges.Count
        };

        return JsonSerializer.Serialize(result, AgentJsonOptions);
    }

    private static List<Node> GetChangedNodes(DiffComputation diff, int maxNodes) =>
        diff.Nodes
            .Where(n => n.Status != "same")
            .OrderByDescending(n => n.RiskScore)
            .ThenBy(n => n.Status)
            .ThenBy(n => n.Id)
            .Take(maxNodes)
            .ToList();

    private static List<ResolvedEdge> GetWireChanges(DiffComputation diff, DiffGraphResolver resolver, int maxEdges) =>
        diff.Edges
            .Where(e => e.Status != "same")
            .OrderBy(e => e.Status)
            .ThenBy(e => e.Source)
            .Take(maxEdges)
            .Select(resolver.Resolve)
            .ToList();

    private static (List<AgentPropertyChange> Text, List<AgentOpaqueChange> Opaque) GetPropertyChanges(IEnumerable<Node> changedNodes)
    {
        var text = new List<AgentPropertyChange>();
        var opaque = new List<AgentOpaqueChange>();

        foreach (var node in changedNodes.Where(n => n.PropertiesOld is { Count: > 0 }))
        {
            var nodeLabel = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
            foreach (var key in node.PropertiesOld!.Keys)
            {
                if (ShouldSkipPropertyKey(key))
                {
                    continue;
                }

                node.Properties.TryGetValue(key, out var newValue);
                node.PropertiesOld.TryGetValue(key, out var oldValue);
                newValue ??= string.Empty;
                oldValue ??= string.Empty;

                if (IsOpaqueProperty(key, oldValue, newValue))
                {
                    opaque.Add(new AgentOpaqueChange
                    {
                        NodeId = node.Id,
                        Node = nodeLabel,
                        Key = key,
                        TypeName = GetPropertyValue(node.Properties, "TypeName"),
                        Hint = GetPropertyValue(node.Properties, "Count") ?? GetPropertyValue(node.Properties, "Path")
                    });
                    continue;
                }

                if (IsTextProperty(key))
                {
                    var (oldTruncated, oldText) = Truncate(oldValue);
                    var (newTruncated, newText) = Truncate(newValue);
                    text.Add(new AgentPropertyChange
                    {
                        NodeId = node.Id,
                        Node = nodeLabel,
                        Key = key,
                        Old = oldText,
                        New = newText,
                        Truncated = oldTruncated || newTruncated
                    });
                }
            }
        }

        return (text, opaque);
    }

    private static List<AgentNodeChange> BuildNodeChanges(
        DiffComputation diff,
        IEnumerable<Node> changedNodes,
        IReadOnlyList<ResolvedEdge> wireChanges,
        DiffGraphResolver resolver)
    {
        var upstream = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var downstream = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var allEdges = diff.Edges;

        foreach (var wire in wireChanges)
        {
            AddNeighbor(downstream, wire.Source, wire.TargetLabel);
            AddNeighbor(upstream, wire.Target, wire.SourceLabel);
        }

        foreach (var node in changedNodes)
        {
            var existingConnections = allEdges
                .Where(e => (e.Source == node.Id || e.Target == node.Id) && e.Status == "same");
            foreach (var edge in existingConnections)
            {
                if (edge.Source == node.Id)
                {
                    var targetLabel = resolver.LabelNode(edge.Target);
                    AddNeighbor(downstream, node.Id, targetLabel);
                }
                if (edge.Target == node.Id)
                {
                    var sourceLabel = resolver.LabelNode(edge.Source);
                    AddNeighbor(upstream, node.Id, sourceLabel);
                }
            }
        }

        return changedNodes.Select(node =>
        {
            var label = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
            upstream.TryGetValue(node.Id, out var up);
            downstream.TryGetValue(node.Id, out var down);

            var category = GetComponentCategory(node.Name);
            var groupContext = GetGroupContext(node);

            return new AgentNodeChange
            {
                NodeId = node.Id,
                Label = label,
                ComponentName = node.Name,
                Category = category,
                Status = node.Status,
                RiskScore = node.RiskScore,
                Reasons = node.RiskReasons,
                GroupContext = groupContext,
                Position = new AgentPosition
                {
                    X = Math.Round(node.X, 1),
                    Y = Math.Round(node.Y, 1)
                },
                Movement = node.Dx != 0 || node.Dy != 0
                    ? new AgentMovement
                    {
                        Dx = Math.Round(node.Dx, 1),
                        Dy = Math.Round(node.Dy, 1)
                    }
                    : null,
                Neighbors = new AgentNeighbors
                {
                    Upstream = up?.OrderBy(x => x, StringComparer.Ordinal).ToList() ?? [],
                    Downstream = down?.OrderBy(x => x, StringComparer.Ordinal).ToList() ?? []
                },
                ConnectionChanges = node.InAdded + node.InRemoved + node.OutAdded + node.OutRemoved > 0
                    ? new AgentConnectionChanges
                    {
                        InAdded = node.InAdded,
                        InRemoved = node.InRemoved,
                        OutAdded = node.OutAdded,
                        OutRemoved = node.OutRemoved
                    }
                    : null
            };
        }).OrderByDescending(n => n.RiskScore).ThenBy(n => n.Status).ThenBy(n => n.Label).ToList();
    }

    private static void AddNeighbor(Dictionary<string, HashSet<string>> map, string nodeId, string neighborLabel)
    {
        if (!map.TryGetValue(nodeId, out var set))
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            map[nodeId] = set;
        }
        set.Add(neighborLabel);
    }

    private static string BuildSummary(
        DiffStatistics stats,
        DiffRiskSummary risks,
        IReadOnlyList<ResolvedEdge> wireChanges,
        IReadOnlyList<AgentPropertyChange> propertyChanges,
        IReadOnlyList<AgentOpaqueChange> opaqueChanges)
    {
        var parts = new List<string>();

        if (stats.AddedNodes == 0 && stats.RemovedNodes == 0 && stats.ModifiedNodes == 0)
        {
            return "No changes detected.";
        }

        var nodeSummary = new List<string>();
        if (stats.AddedNodes > 0) nodeSummary.Add($"{stats.AddedNodes} added");
        if (stats.RemovedNodes > 0) nodeSummary.Add($"{stats.RemovedNodes} removed");
        if (stats.ModifiedNodes > 0) nodeSummary.Add($"{stats.ModifiedNodes} modified");
        parts.Add($"{string.Join(", ", nodeSummary)} nodes");

        if (risks.CriticalCount > 0 || risks.HighCount > 0)
        {
            var riskParts = new List<string>();
            if (risks.CriticalCount > 0) riskParts.Add($"{risks.CriticalCount} critical");
            if (risks.HighCount > 0) riskParts.Add($"{risks.HighCount} high");
            parts.Add($"Risk: {string.Join(", ", riskParts)}");
        }

        var scriptChanges = propertyChanges.Where(p => IsScriptLikeKey(p.Key)).ToList();
        if (scriptChanges.Count > 0)
        {
            parts.Add($"{scriptChanges.Count} script/code changes");
        }

        var wiresByStatus = wireChanges.GroupBy(w => w.Status).ToDictionary(g => g.Key, g => g.Count());
        if (wiresByStatus.Count > 0)
        {
            var wireParts = new List<string>();
            if (wiresByStatus.TryGetValue("added", out var addedWires) && addedWires > 0)
                wireParts.Add($"{addedWires} added");
            if (wiresByStatus.TryGetValue("removed", out var removedWires) && removedWires > 0)
                wireParts.Add($"{removedWires} removed");
            if (wireParts.Count > 0)
                parts.Add($"Connections: {string.Join(", ", wireParts)}");
        }

        var opaqueDataCount = opaqueChanges.Count(p => p.Key.Equals("ON_Data", StringComparison.OrdinalIgnoreCase));
        if (opaqueDataCount > 0)
        {
            parts.Add($"{opaqueDataCount} geometry caches updated");
        }

        return string.Join("; ", parts) + ".";
    }

    private static bool ShouldSkipPropertyKey(string key) =>
        key.Equals("GUID", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Hidden", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Optional", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("SourceCount", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("RefID", StringComparison.OrdinalIgnoreCase);

    private static bool IsTextProperty(string key) =>
        key.Equals("PanelContent", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("UserText", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Text", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Expression", StringComparison.OrdinalIgnoreCase) ||
        IsScriptLikeKey(key);

    private static bool IsOpaqueProperty(string key, string oldValue, string newValue) =>
        key.Equals("ON_Data", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("ClusterHash", StringComparison.OrdinalIgnoreCase) ||
        LooksLikeOpaqueBlob(oldValue) ||
        LooksLikeOpaqueBlob(newValue);

    private static bool LooksLikeOpaqueBlob(string value) =>
        value.Length > 200 && value.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '\n' or '\r');

    private static bool IsScriptLikeKey(string key) =>
        key.Contains("script", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("code", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("python", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("csharp", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("expression", StringComparison.OrdinalIgnoreCase);

    private static string? GetPropertyValue(Dictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value) ? value : null;

    private static (bool Truncated, string Value) Truncate(string value) =>
        value.Length <= TextPropertyMaxLength
            ? (false, value)
            : (true, value[..TextPropertyMaxLength] + "…");

    private static DiffStatistics BuildStatistics(DiffComputation diff) => new()
    {
        TotalNodes = diff.Nodes.Count,
        TotalEdges = diff.Edges.Count,
        AddedNodes = diff.Nodes.Count(n => n.Status == "added"),
        RemovedNodes = diff.Nodes.Count(n => n.Status == "removed"),
        ModifiedNodes = diff.Nodes.Count(n => n.Status == "modified"),
        UnchangedNodes = diff.Nodes.Count(n => n.Status == "same"),
        AddedEdges = diff.Edges.Count(e => e.Status == "added"),
        RemovedEdges = diff.Edges.Count(e => e.Status == "removed"),
        NodesWithConnectionChanges = diff.Nodes.Count(n =>
            n.InAdded > 0 || n.InRemoved > 0 || n.OutAdded > 0 || n.OutRemoved > 0)
    };

    private static string? GetComponentCategory(string componentName)
    {
        if (string.IsNullOrWhiteSpace(componentName)) return null;

        var categoryMap = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["Params"] = ["Number Slider", "Panel", "Number", "Point", "Vector", "Integer", "Boolean", "Text", "Curve", "Surface", "Brep", "Mesh", "Geometry"],
            ["Maths"] = ["Addition", "Subtraction", "Multiplication", "Division", "Modulus", "Power", "Expression", "Function", "Evaluate"],
            ["Sets"] = ["List Item", "List Length", "Reverse", "Sort", "Cull", "Shift", "Split", "Merge", "Flatten", "Graft", "Simplify"],
            ["Vector"] = ["Unit X", "Unit Y", "Unit Z", "Vector", "Amplitude", "Rotate", "Angle"],
            ["Curve"] = ["Line", "Circle", "Arc", "Polyline", "Interpolate", "Bezier", "Nurbs", "Divide", "Evaluate"],
            ["Surface"] = ["Plane Surface", "Boundary Surface", "Extrude", "Loft", "Sweep", "Revolve", "Patch"],
            ["Mesh"] = ["Mesh Sphere", "Mesh Box", "Mesh Quad", "Mesh Triangle", "Mesh Edges", "Mesh Vertices"],
            ["Intersect"] = ["Line | Line", "Curve | Curve", "Brep | Brep", "Plane | Plane", "Region Intersection"],
            ["Transform"] = ["Move", "Rotate", "Scale", "Mirror", "Orient", "Project", "Morph", "Array"],
            ["Display"] = ["Custom Preview", "Text Tag", "Point List", "Curve", "Surface", "Mesh", "Geometry"],
            ["Script"] = ["Python", "C#", "VB", "Grasshopper", "Expression"]
        };

        foreach (var (category, components) in categoryMap)
        {
            if (components.Any(c => componentName.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                return category;
            }
        }

        if (componentName.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("Python", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("C#", StringComparison.OrdinalIgnoreCase) ||
            componentName.Contains("VB", StringComparison.OrdinalIgnoreCase))
        {
            return "Script";
        }

        return null;
    }

    private static AgentGroupContext? GetGroupContext(Node node)
    {
        if (!node.Properties.TryGetValue("IsGroup", out var isGroup) || isGroup != "true")
        {
            return null;
        }

        var context = new AgentGroupContext();

        if (node.Properties.TryGetValue("GroupMemberCount", out var countStr) &&
            int.TryParse(countStr, out var count))
        {
            context.MemberCount = count;
        }

        if (node.Properties.TryGetValue("GroupColor", out var color))
        {
            context.Color = color;
        }

        return context;
    }

    private static readonly JsonSerializerOptions AgentJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

internal sealed class AgentDiffReport
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string? FileOld { get; set; }
    public string? FileNew { get; set; }
    public string Summary { get; set; } = string.Empty;
    public DiffStatistics Statistics { get; set; } = new();
    public DiffRiskSummary RiskSummary { get; set; } = new();
    public List<NodeRiskFinding> TopRisks { get; set; } = new();
    public List<ResolvedEdge> WireChanges { get; set; } = new();
    public List<AgentPropertyChange> PropertyChanges { get; set; } = new();
    public List<AgentOpaqueChange> OpaqueChanges { get; set; } = new();
    public List<AgentNodeChange> NodeChanges { get; set; } = new();
    public List<DiffDiagnostic> Diagnostics { get; set; } = new();
    public int TotalChangedNodes { get; set; }
    public int TotalChangedEdges { get; set; }
    public bool TruncatedNodes { get; set; }
    public bool TruncatedEdges { get; set; }
}

internal sealed class AgentPropertyChange
{
    public string NodeId { get; set; } = string.Empty;
    public string Node { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Old { get; set; } = string.Empty;
    public string New { get; set; } = string.Empty;
    public bool Truncated { get; set; }
}

internal sealed class AgentOpaqueChange
{
    public string NodeId { get; set; } = string.Empty;
    public string Node { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public string? Hint { get; set; }
}

internal sealed class AgentNodeChange
{
    public string NodeId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public List<string> Reasons { get; set; } = new();
    public AgentGroupContext? GroupContext { get; set; }
    public AgentPosition Position { get; set; } = new();
    public AgentMovement? Movement { get; set; }
    public AgentNeighbors Neighbors { get; set; } = new();
    public AgentConnectionChanges? ConnectionChanges { get; set; }
}

internal sealed class AgentNeighbors
{
    public List<string> Upstream { get; set; } = new();
    public List<string> Downstream { get; set; } = new();
}

internal sealed class AgentGroupContext
{
    public int MemberCount { get; set; }
    public string? Color { get; set; }
}

internal sealed class AgentPosition
{
    public double X { get; set; }
    public double Y { get; set; }
}

internal sealed class AgentMovement
{
    public double Dx { get; set; }
    public double Dy { get; set; }
}

internal sealed class AgentConnectionChanges
{
    public int InAdded { get; set; }
    public int InRemoved { get; set; }
    public int OutAdded { get; set; }
    public int OutRemoved { get; set; }
}
