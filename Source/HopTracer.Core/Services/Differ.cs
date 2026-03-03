using HopTracer.Core.Models;
using Microsoft.Extensions.Logging;

namespace HopTracer.Core.Services;

/// <summary>
/// Computes the difference between two Grasshopper graphs.
/// </summary>
public class Differ : IDiffer
{
    private readonly ILogger<Differ> _logger;
    private const double MovementComparisonTolerance = 0.05;
    private static readonly HashSet<string> ScriptPropertyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "ScriptSource", "Code", "Script", "Expression",
        "CodeInput", "CodeOutput", "PythonScript", "PythonCode",
        "CSharpCode", "VBCode", "SourceCode", "ScriptBody"
    };
    private static readonly string[] ClusterPreviewPropertyKeys =
    {
        "ClusterHash", "clusterHash",
        "ClusterSize", "clusterSize",
        "ClusterPreviewStatus", "clusterPreviewStatus",
        "ClusterPreviewMessage", "clusterPreviewMessage",
        "ClusterPreviewGraph", "clusterPreviewGraph"
    };

    public Differ(ILogger<Differ> logger)
    {
        _logger = logger;
    }

    public (List<Node> Nodes, List<Edge> Edges) Diff(Graph oldGraph, Graph newGraph)
    {
        var detailed = DiffDetailed(oldGraph, newGraph);
        return (detailed.Nodes, detailed.Edges);
    }

    public DiffComputation DiffDetailed(Graph oldGraph, Graph newGraph)
    {
        _logger.LogInformation("Starting diff between graphs. Old nodes: {OldCount}, New nodes: {NewCount}", oldGraph.Nodes.Count, newGraph.Nodes.Count);

        var result = new DiffComputation();
        var matches = BuildNodeMatches(oldGraph, newGraph);

        if (matches.FallbackMatchedCount > 0)
        {
            AddDiagnostic(result.Diagnostics, "NODE_IDENTITY_FALLBACK", "warning",
                $"Matched {matches.FallbackMatchedCount} node(s) with fallback identity logic after GUID churn.");
        }

        foreach (var pair in matches.OldToNew.OrderBy(p => p.Value, StringComparer.Ordinal).ThenBy(p => p.Key, StringComparer.Ordinal))
        {
            var nOld = oldGraph.Nodes[pair.Key];
            var nNew = newGraph.Nodes[pair.Value];
            var merged = BuildMergedNode(nOld, nNew);
            ScoreNodeRisk(merged, nOld, nNew);
            result.Nodes.Add(merged);
        }

        var matchedOldIds = matches.OldToNew.Keys.ToHashSet(StringComparer.Ordinal);
        var matchedNewIds = matches.OldToNew.Values.ToHashSet(StringComparer.Ordinal);

        foreach (var n in newGraph.Nodes.Values.Where(n => !matchedNewIds.Contains(n.Id)).OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var added = CloneNode(n);
            added.Status = "added";
            ScoreNodeRisk(added, null, n);
            result.Nodes.Add(added);
        }

        foreach (var n in oldGraph.Nodes.Values.Where(n => !matchedOldIds.Contains(n.Id)).OrderBy(n => n.Id, StringComparer.Ordinal))
        {
            var removed = CloneNode(n);
            removed.Status = "removed";
            ScoreNodeRisk(removed, n, null);
            result.Nodes.Add(removed);
        }

        var edgeComparer = new EdgeComparer();
        var oldCanonicalEdges = oldGraph.Edges
            .Select(e => CanonicalizeEdge(e, matches.OldToNew))
            .ToHashSet(edgeComparer);
        var newCanonicalEdges = newGraph.Edges
            .Select(CloneEdge)
            .ToHashSet(edgeComparer);
        var allCanonicalEdges = oldCanonicalEdges.Union(newCanonicalEdges, edgeComparer);

        var nodeIoStats = result.Nodes.ToDictionary(
            n => n.Id,
            _ => (InAdded: 0, InRemoved: 0, OutAdded: 0, OutRemoved: 0),
            StringComparer.Ordinal);

        var danglingEndpoints = 0;
        foreach (var edge in allCanonicalEdges)
        {
            var isOld = oldCanonicalEdges.Contains(edge);
            var isNew = newCanonicalEdges.Contains(edge);

            var status = "same";
            if (isOld && !isNew)
            {
                status = "removed";
            }
            else if (!isOld && isNew)
            {
                status = "added";
            }

            if (status == "added")
            {
                UpdateStats(nodeIoStats, edge.Target, stats => (stats.InAdded + 1, stats.InRemoved, stats.OutAdded, stats.OutRemoved));
                UpdateStats(nodeIoStats, edge.Source, stats => (stats.InAdded, stats.InRemoved, stats.OutAdded + 1, stats.OutRemoved));
            }
            else if (status == "removed")
            {
                UpdateStats(nodeIoStats, edge.Target, stats => (stats.InAdded, stats.InRemoved + 1, stats.OutAdded, stats.OutRemoved));
                UpdateStats(nodeIoStats, edge.Source, stats => (stats.InAdded, stats.InRemoved, stats.OutAdded, stats.OutRemoved + 1));
            }

            if (!nodeIoStats.ContainsKey(edge.Source) || !nodeIoStats.ContainsKey(edge.Target))
            {
                danglingEndpoints++;
            }

            result.Edges.Add(new Edge
            {
                Source = edge.Source,
                SourcePort = edge.SourcePort,
                Target = edge.Target,
                TargetPort = edge.TargetPort,
                Status = status
            });
        }

        foreach (var node in result.Nodes)
        {
            if (nodeIoStats.TryGetValue(node.Id, out var stats))
            {
                node.InAdded = stats.InAdded;
                node.InRemoved = stats.InRemoved;
                node.OutAdded = stats.OutAdded;
                node.OutRemoved = stats.OutRemoved;
                var hasConnectionChanges = (stats.InAdded + stats.InRemoved + stats.OutAdded + stats.OutRemoved) > 0;

                if (node.Status == "same" &&
                    hasConnectionChanges)
                {
                    node.Status = "modified";
                }

                if (!hasConnectionChanges)
                {
                    continue;
                }

                if (node.RiskScore == 0)
                {
                    node.RiskScore = 15;
                }

                if (!node.RiskReasons.Contains("Connection changes"))
                {
                    node.RiskReasons.Add("Connection changes");
                }
            }
        }

        foreach (var node in result.Nodes)
        {
            if (!string.Equals(node.Status, "modified", StringComparison.Ordinal) || node.RiskScore > 0)
            {
                continue;
            }

            node.RiskScore = 15;
            if (!node.RiskReasons.Contains("Node modified"))
            {
                node.RiskReasons.Add("Node modified");
            }
        }

        if (danglingEndpoints > 0)
        {
            AddDiagnostic(result.Diagnostics, "DANGLING_EDGE_ENDPOINTS", "warning",
                $"{danglingEndpoints} edge(s) reference node ids not present in diff output.");
        }

        AppendGraphDiagnostics(result.Diagnostics, oldGraph.Metadata, "Old");
        AppendGraphDiagnostics(result.Diagnostics, newGraph.Metadata, "New");

        result.TopRisks = result.Nodes
            .Where(n => n.RiskScore > 0)
            .OrderByDescending(n => n.RiskScore)
            .ThenBy(n => n.Status, StringComparer.Ordinal)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .Take(10)
            .Select(n => new NodeRiskFinding
            {
                NodeId = n.Id,
                NodeName = string.IsNullOrWhiteSpace(n.Nickname) ? n.Name : n.Nickname,
                NodeStatus = n.Status,
                RiskScore = n.RiskScore,
                Reasons = n.RiskReasons.Take(5).ToList()
            })
            .ToList();

        result.RiskSummary = BuildRiskSummary(result.Nodes);

        _logger.LogInformation("Diff complete. Found {DiffNodeCount} nodes and {DiffEdgeCount} edges.", result.Nodes.Count, result.Edges.Count);
        return result;
    }

    private static DiffRiskSummary BuildRiskSummary(IEnumerable<Node> nodes)
    {
        var summary = new DiffRiskSummary();
        foreach (var node in nodes)
        {
            if (node.RiskScore >= 80) summary.CriticalCount++;
            else if (node.RiskScore >= 60) summary.HighCount++;
            else if (node.RiskScore >= 30) summary.MediumCount++;
            else if (node.RiskScore > 0) summary.LowCount++;
        }

        return summary;
    }

    private static void AppendGraphDiagnostics(List<DiffDiagnostic> diagnostics, GraphMetadata meta, string side)
    {
        if (meta.UnresolvedEdgeReferences > 0)
        {
            diagnostics.Add(new DiffDiagnostic
            {
                Code = "UNRESOLVED_EDGE_REFERENCE",
                Severity = "warning",
                Message = $"{side} graph contains {meta.UnresolvedEdgeReferences} unresolved edge reference(s)."
            });
        }

        if (meta.ClusterPreviewFailed > 0)
        {
            diagnostics.Add(new DiffDiagnostic
            {
                Code = "CLUSTER_PREVIEW_FAILED",
                Severity = "warning",
                Message = $"{side} graph failed to decode {meta.ClusterPreviewFailed} cluster preview payload(s)."
            });
        }

        if (meta.ClusterPreviewDepthLimitHits > 0)
        {
            diagnostics.Add(new DiffDiagnostic
            {
                Code = "CLUSTER_PREVIEW_DEPTH_LIMIT",
                Severity = "info",
                Message = $"{side} graph hit cluster preview depth limit {meta.ClusterPreviewDepthLimitHits} time(s)."
            });
        }
    }

    private static void AddDiagnostic(List<DiffDiagnostic> diagnostics, string code, string severity, string message)
    {
        diagnostics.Add(new DiffDiagnostic
        {
            Code = code,
            Severity = severity,
            Message = message
        });
    }

    private static Node CloneNode(Node src)
    {
        return new Node
        {
            Id = src.Id,
            Name = src.Name,
            Nickname = src.Nickname,
            X = src.X,
            Y = src.Y,
            W = src.W,
            H = src.H,
            Status = src.Status,
            Dx = src.Dx,
            Dy = src.Dy,
            InAdded = src.InAdded,
            InRemoved = src.InRemoved,
            OutAdded = src.OutAdded,
            OutRemoved = src.OutRemoved,
            Inputs = src.Inputs.Select(ClonePort).ToList(),
            Outputs = src.Outputs.Select(ClonePort).ToList(),
            Properties = new Dictionary<string, string>(src.Properties, StringComparer.Ordinal),
            PropertiesOld = src.PropertiesOld == null ? null : new Dictionary<string, string>(src.PropertiesOld, StringComparer.Ordinal),
            RiskScore = src.RiskScore,
            RiskReasons = src.RiskReasons.ToList()
        };
    }

    private static Port ClonePort(Port src)
    {
        return new Port
        {
            Id = src.Id,
            Name = src.Name,
            Nickname = src.Nickname,
            Kind = src.Kind,
            Type = src.Type,
            Value = src.Value,
            Status = src.Status,
            ValueChanged = src.ValueChanged,
            ValueOld = src.ValueOld,
            ValueNew = src.ValueNew,
            WireDisplay = src.WireDisplay
        };
    }

    private static Edge CloneEdge(Edge src)
    {
        return new Edge
        {
            Source = src.Source,
            SourcePort = src.SourcePort,
            Target = src.Target,
            TargetPort = src.TargetPort,
            Status = src.Status
        };
    }

    private Node BuildMergedNode(Node nOld, Node nNew)
    {
        var dx = NormalizeMovement(nNew.X - nOld.X);
        var dy = NormalizeMovement(nNew.Y - nOld.Y);
        var modified = nOld.Name != nNew.Name || nOld.Nickname != nNew.Nickname;

        var mergedInputs = DiffPorts(nOld.Inputs, nNew.Inputs);
        var mergedOutputs = DiffPorts(nOld.Outputs, nNew.Outputs);

        var properties = new Dictionary<string, string>(nNew.Properties, StringComparer.Ordinal);
        var propertiesOld = new Dictionary<string, string>(StringComparer.Ordinal);
        var propertiesChanged = false;
        var includeScriptComparisonBaseline = false;

        foreach (var kv in nOld.Properties)
        {
            if (!ContainsKeyIgnoreCase(properties, kv.Key))
            {
                properties[kv.Key] = kv.Value;
            }

            if (TryGetValueIgnoreCase(nNew.Properties, kv.Key, out var newKey, out var newVal) && kv.Value != newVal)
            {
                modified = true;
                propertiesChanged = true;
                propertiesOld[newKey] = kv.Value;
            }
        }

        ReconcileScriptProperties(
            nOld.Properties,
            nNew.Properties,
            properties,
            propertiesOld,
            modified,
            ref includeScriptComparisonBaseline,
            ref propertiesChanged,
            ref modified);

        if (mergedInputs.Any(p => p.Status != "same" || p.ValueChanged) ||
            mergedOutputs.Any(p => p.Status != "same" || p.ValueChanged))
        {
            modified = true;
        }

        if (HasClusterHashChange(nOld.Properties, nNew.Properties))
        {
            modified = true;
            propertiesChanged = true;
            PreserveClusterPreviewOldValues(propertiesOld, nOld.Properties);
        }

        return new Node
        {
            Id = nNew.Id,
            Name = nNew.Name,
            Nickname = nNew.Nickname,
            X = nNew.X,
            Y = nNew.Y,
            W = nNew.W,
            H = nNew.H,
            Status = modified ? "modified" : "same",
            Dx = dx,
            Dy = dy,
            Inputs = mergedInputs,
            Outputs = mergedOutputs,
            Properties = properties,
            PropertiesOld = (propertiesChanged || includeScriptComparisonBaseline) ? propertiesOld : null
        };
    }

    private static void ReconcileScriptProperties(
        Dictionary<string, string> oldProps,
        Dictionary<string, string> newProps,
        Dictionary<string, string> mergedProps,
        Dictionary<string, string> oldValuesOut,
        bool nodeAlreadyModified,
        ref bool includeScriptComparisonBaseline,
        ref bool propertiesChanged,
        ref bool modified)
    {
        // Keep baseline script values on modified nodes so UI can distinguish unchanged vs added/removed script keys.
        if (!nodeAlreadyModified && !HasScriptChange(oldProps, newProps))
        {
            return;
        }

        var oldScriptKeys = BuildScriptKeyLookup(oldProps);
        var newScriptKeys = BuildScriptKeyLookup(newProps);
        var union = new HashSet<string>(oldScriptKeys.Keys, StringComparer.OrdinalIgnoreCase);
        union.UnionWith(newScriptKeys.Keys);

        foreach (var key in union)
        {
            var hasOld = TryResolveValue(oldProps, oldScriptKeys, key, out _, out var oldVal);
            var hasNew = TryResolveValue(newProps, newScriptKeys, key, out var newResolvedKey, out var newVal);

            var displayKey = hasNew ? newResolvedKey : (oldScriptKeys.TryGetValue(key, out var oldResolvedKey) ? oldResolvedKey : key);
            var normalizedOld = hasOld ? oldVal : string.Empty;
            var normalizedNew = hasNew ? newVal : string.Empty;

            mergedProps[displayKey] = normalizedNew;
            oldValuesOut[displayKey] = normalizedOld;
            includeScriptComparisonBaseline = true;

            if (!string.Equals(normalizedOld, normalizedNew, StringComparison.Ordinal))
            {
                modified = true;
                propertiesChanged = true;
            }
        }
    }

    private static Dictionary<string, string> BuildScriptKeyLookup(Dictionary<string, string> props)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in props.Keys)
        {
            if (!IsScriptLikeKey(key))
            {
                continue;
            }

            if (!map.ContainsKey(key))
            {
                map[key] = key;
            }
        }

        return map;
    }

    private static bool TryResolveValue(
        Dictionary<string, string> props,
        Dictionary<string, string> keyLookup,
        string key,
        out string resolvedKey,
        out string resolvedValue)
    {
        if (keyLookup.TryGetValue(key, out var mappedKey) &&
            props.TryGetValue(mappedKey, out var directValue))
        {
            resolvedKey = mappedKey;
            resolvedValue = directValue ?? string.Empty;
            return true;
        }

        if (TryGetValueIgnoreCase(props, key, out var fallbackKey, out var fallbackValue))
        {
            resolvedKey = fallbackKey;
            resolvedValue = fallbackValue;
            return true;
        }

        resolvedKey = key;
        resolvedValue = string.Empty;
        return false;
    }

    private static bool ContainsKeyIgnoreCase(Dictionary<string, string> props, string key)
    {
        return props.Keys.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetValueIgnoreCase(
        Dictionary<string, string> props,
        string key,
        out string resolvedKey,
        out string resolvedValue)
    {
        foreach (var kv in props)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                resolvedKey = kv.Key;
                resolvedValue = kv.Value ?? string.Empty;
                return true;
            }
        }

        resolvedKey = key;
        resolvedValue = string.Empty;
        return false;
    }

    private static void PreserveClusterPreviewOldValues(Dictionary<string, string> destination, Dictionary<string, string> source)
    {
        foreach (var key in ClusterPreviewPropertyKeys)
        {
            if (source.TryGetValue(key, out var value))
            {
                destination[key] = value;
            }
        }
    }

    private List<Port> DiffPorts(List<Port> oldPorts, List<Port> newPorts)
    {
        var result = new List<Port>();
        var allIds = oldPorts.Select(p => p.Id).Union(newPorts.Select(p => p.Id), StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

        foreach (var pid in allIds)
        {
            var inOld = oldPorts.FirstOrDefault(p => p.Id == pid);
            var inNew = newPorts.FirstOrDefault(p => p.Id == pid);

            if (inOld != null && inNew != null)
            {
                var valueChanged = (inOld.Value ?? string.Empty) != (inNew.Value ?? string.Empty);
                var status = valueChanged ||
                    inOld.Name != inNew.Name ||
                    inOld.Nickname != inNew.Nickname ||
                    inOld.WireDisplay != inNew.WireDisplay
                    ? "modified"
                    : "same";

                result.Add(new Port
                {
                    Id = pid,
                    Name = inNew.Name,
                    Nickname = inNew.Nickname,
                    Kind = inNew.Kind,
                    Type = inNew.Type,
                    Value = inNew.Value,
                    ValueOld = inOld.Value,
                    ValueNew = inNew.Value,
                    ValueChanged = valueChanged,
                    Status = status,
                    WireDisplay = inNew.WireDisplay
                });
            }
            else if (inNew != null)
            {
                var clone = ClonePort(inNew);
                clone.Status = "added";
                result.Add(clone);
            }
            else if (inOld != null)
            {
                var clone = ClonePort(inOld);
                clone.Status = "removed";
                result.Add(clone);
            }
        }

        return result.OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
    }

    private static void UpdateStats(
        Dictionary<string, (int InAdded, int InRemoved, int OutAdded, int OutRemoved)> statsDict,
        string id,
        Func<(int InAdded, int InRemoved, int OutAdded, int OutRemoved), (int, int, int, int)> updateFunc)
    {
        if (statsDict.TryGetValue(id, out var currentStats))
        {
            statsDict[id] = updateFunc(currentStats);
        }
    }

    private static Edge CanonicalizeEdge(Edge edge, Dictionary<string, string> oldToNew)
    {
        var canonicalSource = oldToNew.TryGetValue(edge.Source, out var mappedSource) ? mappedSource : edge.Source;
        var canonicalTarget = oldToNew.TryGetValue(edge.Target, out var mappedTarget) ? mappedTarget : edge.Target;

        return new Edge
        {
            Source = canonicalSource,
            Target = canonicalTarget,
            SourcePort = CanonicalizePort(edge.SourcePort, edge.Source, canonicalSource),
            TargetPort = CanonicalizePort(edge.TargetPort, edge.Target, canonicalTarget),
            Status = edge.Status
        };
    }

    private static string CanonicalizePort(string portId, string originalNodeId, string canonicalNodeId)
    {
        if (string.IsNullOrEmpty(portId) || originalNodeId == canonicalNodeId)
        {
            return portId;
        }

        if (portId.StartsWith(originalNodeId + ":", StringComparison.Ordinal))
        {
            return canonicalNodeId + portId[originalNodeId.Length..];
        }

        return portId;
    }

    private static void ScoreNodeRisk(Node target, Node? oldNode, Node? newNode)
    {
        var score = 0;
        var reasons = new List<string>();

        if (target.Status == "removed")
        {
            score += 85;
            reasons.Add("Node removed");
        }
        else if (target.Status == "added")
        {
            score += 65;
            reasons.Add("Node added");
        }
        else if (target.Status == "modified")
        {
            score += 35;
            reasons.Add("Node modified");
        }

        var oldProps = oldNode?.Properties ?? new Dictionary<string, string>(StringComparer.Ordinal);
        var newProps = newNode?.Properties ?? target.Properties;

        if (HasScriptChange(oldProps, newProps))
        {
            score += 40;
            reasons.Add("Script/code change");
        }

        if (HasClusterHashChange(oldProps, newProps))
        {
            score += 40;
            reasons.Add("Cluster internals changed");
        }

        if (HasSignificantMovement(target.Dx, target.Dy))
        {
            score += 10;
            reasons.Add("Component moved");
        }

        score = Math.Min(score, 100);
        target.RiskScore = score;
        target.RiskReasons = reasons;
    }

    private static bool HasSignificantMovement(double dx, double dy)
    {
        return Math.Abs(dx) >= MovementComparisonTolerance || Math.Abs(dy) >= MovementComparisonTolerance;
    }

    private static double NormalizeMovement(double delta)
    {
        return Math.Abs(delta) < MovementComparisonTolerance ? 0 : delta;
    }

    private static bool HasClusterHashChange(Dictionary<string, string> oldProps, Dictionary<string, string> newProps)
    {
        var oldHash = GetProp(oldProps, "ClusterHash");
        var newHash = GetProp(newProps, "ClusterHash");
        return !string.IsNullOrEmpty(oldHash) && !string.IsNullOrEmpty(newHash) && !string.Equals(oldHash, newHash, StringComparison.Ordinal);
    }

    private static bool HasScriptChange(Dictionary<string, string> oldProps, Dictionary<string, string> newProps)
    {
        foreach (var key in ScriptPropertyKeys)
        {
            var oldVal = GetProp(oldProps, key);
            var newVal = GetProp(newProps, key);
            if (oldVal != null && newVal != null && oldVal != newVal)
            {
                return true;
            }
        }

        var keyUnion = oldProps.Keys.Union(newProps.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var key in keyUnion.Where(IsScriptLikeKey))
        {
            oldProps.TryGetValue(key, out var oldVal);
            newProps.TryGetValue(key, out var newVal);
            if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsScriptLikeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        return key.Contains("script", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("code", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("python", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("csharp", StringComparison.OrdinalIgnoreCase) ||
               key.Contains("expression", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetProp(Dictionary<string, string> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            return value;
        }

        var alt = char.ToLowerInvariant(key[0]) + key[1..];
        return props.TryGetValue(alt, out var altValue) ? altValue : null;
    }

    private static NodeMatchResult BuildNodeMatches(Graph oldGraph, Graph newGraph)
    {
        var oldToNew = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var id in oldGraph.Nodes.Keys.Intersect(newGraph.Nodes.Keys, StringComparer.Ordinal))
        {
            oldToNew[id] = id;
        }

        var unmatchedOld = oldGraph.Nodes.Values
            .Where(n => !oldToNew.ContainsKey(n.Id))
            .OrderBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        var unmatchedNew = newGraph.Nodes.Values
            .Where(n => !oldToNew.ContainsValue(n.Id))
            .OrderBy(n => n.Id, StringComparer.Ordinal)
            .ToList();

        var usedNew = new HashSet<string>(oldToNew.Values, StringComparer.Ordinal);
        var fallbackCount = 0;

        foreach (var oldNode in unmatchedOld)
        {
            Node? best = null;
            var bestScore = 0;

            foreach (var candidate in unmatchedNew)
            {
                if (usedNew.Contains(candidate.Id))
                {
                    continue;
                }

                var score = IdentityScore(oldNode, candidate);
                if (score > bestScore || (score == bestScore && best != null &&
                    string.CompareOrdinal(candidate.Id, best.Id) < 0))
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (best != null && bestScore >= 60)
            {
                oldToNew[oldNode.Id] = best.Id;
                usedNew.Add(best.Id);
                fallbackCount++;
            }
        }

        return new NodeMatchResult
        {
            OldToNew = oldToNew,
            FallbackMatchedCount = fallbackCount
        };
    }

    private static int IdentityScore(Node oldNode, Node newNode)
    {
        var score = 0;

        if (string.Equals(oldNode.Name, newNode.Name, StringComparison.OrdinalIgnoreCase))
        {
            score += 35;
        }

        if (!string.IsNullOrWhiteSpace(oldNode.Nickname) &&
            string.Equals(oldNode.Nickname, newNode.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            score += 20;
        }

        if (oldNode.Inputs.Count == newNode.Inputs.Count)
        {
            score += 10;
        }

        if (oldNode.Outputs.Count == newNode.Outputs.Count)
        {
            score += 10;
        }

        if (BuildPortSchema(oldNode) == BuildPortSchema(newNode))
        {
            score += 15;
        }

        var distance = Math.Sqrt(Math.Pow(newNode.X - oldNode.X, 2) + Math.Pow(newNode.Y - oldNode.Y, 2));
        if (distance <= 50) score += 20;
        else if (distance <= 150) score += 10;
        else if (distance <= 400) score += 5;

        var oldHash = GetProp(oldNode.Properties, "ClusterHash");
        var newHash = GetProp(newNode.Properties, "ClusterHash");
        if (!string.IsNullOrEmpty(oldHash) && !string.IsNullOrEmpty(newHash))
        {
            score += string.Equals(oldHash, newHash, StringComparison.Ordinal) ? 25 : 10;
        }

        if (HasScriptChange(oldNode.Properties, newNode.Properties))
        {
            score += 5;
        }
        else if (ScriptPropertyKeys.Any(k => oldNode.Properties.ContainsKey(k) && newNode.Properties.ContainsKey(k)))
        {
            score += 20;
        }

        return score;
    }

    private static string BuildPortSchema(Node node)
    {
        var inputs = string.Join("|", node.Inputs.Select(p => (p.Name ?? p.Nickname ?? string.Empty).Trim().ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal));
        var outputs = string.Join("|", node.Outputs.Select(p => (p.Name ?? p.Nickname ?? string.Empty).Trim().ToLowerInvariant()).OrderBy(x => x, StringComparer.Ordinal));
        return $"{inputs}>>{outputs}";
    }

    private sealed class NodeMatchResult
    {
        public Dictionary<string, string> OldToNew { get; set; } = new(StringComparer.Ordinal);
        public int FallbackMatchedCount { get; set; }
    }
}

internal class EdgeComparer : IEqualityComparer<Edge>
{
    public bool Equals(Edge? x, Edge? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;

        return string.Equals(NormalizeEdgeToken(x.Source), NormalizeEdgeToken(y.Source), StringComparison.Ordinal) &&
               string.Equals(NormalizeEdgeToken(x.SourcePort), NormalizeEdgeToken(y.SourcePort), StringComparison.Ordinal) &&
               string.Equals(NormalizeEdgeToken(x.Target), NormalizeEdgeToken(y.Target), StringComparison.Ordinal) &&
               string.Equals(NormalizeEdgeToken(x.TargetPort), NormalizeEdgeToken(y.TargetPort), StringComparison.Ordinal);
    }

    public int GetHashCode(Edge obj)
    {
        return HashCode.Combine(
            NormalizeEdgeToken(obj.Source),
            NormalizeEdgeToken(obj.SourcePort),
            NormalizeEdgeToken(obj.Target),
            NormalizeEdgeToken(obj.TargetPort));
    }

    private static string NormalizeEdgeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        return token.Trim().Replace("{", string.Empty, StringComparison.Ordinal).Replace("}", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }
}
