using HopTracer.Core.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HopTracer.Core.Services;

public static class DiffAgentFormatter
{
    private const int TextPropertyMaxLength = 500;

    public static string Generate(DiffComputation diff, DiffOutputOptions options)
    {
        var resolver = new DiffGraphResolver(diff.Nodes);
        var stats = BuildStatistics(diff);
        var changedNodes = GetChangedNodes(diff, options.MaxNodesToShow);
        var wireChanges = GetWireChanges(diff, resolver, options.MaxEdgesToShow);
        var (propertyChanges, opaqueChanges) = GetPropertyChanges(changedNodes);
        var nodeChanges = BuildNodeChanges(changedNodes, wireChanges);

        var result = new AgentDiffReport
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            FileOld = options.FileOld,
            FileNew = options.FileNew,
            Summary = BuildSummary(stats, wireChanges, propertyChanges, opaqueChanges),
            Statistics = stats,
            RiskSummary = diff.RiskSummary,
            TopRisks = diff.TopRisks,
            WireChanges = wireChanges,
            PropertyChanges = propertyChanges,
            OpaqueChanges = opaqueChanges,
            NodeChanges = nodeChanges,
            Diagnostics = diff.Diagnostics
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
                        Node = nodeLabel,
                        Key = key,
                        TypeName = GetPropertyValue(node.Properties, "TypeName"),
                        Hint = GetPropertyValue(node.Properties, "Count") ?? GetPropertyValue(node.Properties, "Path")
                    });
                    continue;
                }

                if (IsTextProperty(key))
                {
                    text.Add(new AgentPropertyChange
                    {
                        Node = nodeLabel,
                        Key = key,
                        Old = Truncate(oldValue),
                        New = Truncate(newValue)
                    });
                }
            }
        }

        return (text, opaque);
    }

    private static List<AgentNodeChange> BuildNodeChanges(
        IEnumerable<Node> changedNodes,
        IReadOnlyList<ResolvedEdge> wireChanges)
    {
        var upstream = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var downstream = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var wire in wireChanges)
        {
            AddNeighbor(downstream, wire.Source, wire.TargetLabel);
            AddNeighbor(upstream, wire.Target, wire.SourceLabel);
            if (wire.Status == "removed")
            {
                AddNeighbor(downstream, wire.Source, wire.TargetLabel);
                AddNeighbor(upstream, wire.Target, wire.SourceLabel);
            }
        }

        return changedNodes.Select(node =>
        {
            var label = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
            upstream.TryGetValue(node.Id, out var up);
            downstream.TryGetValue(node.Id, out var down);

            return new AgentNodeChange
            {
                Label = label,
                Status = node.Status,
                RiskScore = node.RiskScore,
                Reasons = node.RiskReasons,
                Neighbors = new AgentNeighbors
                {
                    Upstream = up?.OrderBy(x => x, StringComparer.Ordinal).ToList() ?? [],
                    Downstream = down?.OrderBy(x => x, StringComparer.Ordinal).ToList() ?? []
                }
            };
        }).ToList();
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
        IReadOnlyList<ResolvedEdge> wireChanges,
        IReadOnlyList<AgentPropertyChange> propertyChanges,
        IReadOnlyList<AgentOpaqueChange> opaqueChanges)
    {
        var parts = new List<string>
        {
            $"{stats.AddedNodes} added, {stats.RemovedNodes} removed, {stats.ModifiedNodes} modified nodes"
        };

        foreach (var wire in wireChanges.Take(3))
        {
            parts.Add($"{wire.Status} wire {wire.Wire}");
        }

        var panelChange = propertyChanges.FirstOrDefault(p =>
            p.Key.Contains("Panel", StringComparison.OrdinalIgnoreCase) ||
            p.Key.Contains("Text", StringComparison.OrdinalIgnoreCase));
        if (panelChange != null)
        {
            parts.Add($"{panelChange.Node}.{panelChange.Key} updated");
        }

        var scriptChange = propertyChanges.FirstOrDefault(p => IsScriptLikeKey(p.Key));
        if (scriptChange != null)
        {
            parts.Add($"{scriptChange.Node} script updated");
        }

        var opaqueDataCount = opaqueChanges.Count(p => p.Key.Equals("ON_Data", StringComparison.OrdinalIgnoreCase));
        if (opaqueDataCount > 0)
        {
            parts.Add($"{opaqueDataCount} geometry/data caches updated");
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

    private static string Truncate(string value) =>
        value.Length <= TextPropertyMaxLength ? value : value[..TextPropertyMaxLength] + "…";

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
}

internal sealed class AgentPropertyChange
{
    public string Node { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Old { get; set; } = string.Empty;
    public string New { get; set; } = string.Empty;
}

internal sealed class AgentOpaqueChange
{
    public string Node { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? TypeName { get; set; }
    public string? Hint { get; set; }
}

internal sealed class AgentNodeChange
{
    public string Label { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public List<string> Reasons { get; set; } = new();
    public AgentNeighbors Neighbors { get; set; } = new();
}

internal sealed class AgentNeighbors
{
    public List<string> Upstream { get; set; } = new();
    public List<string> Downstream { get; set; } = new();
}
