using HopTracer.Core.Models;
using Microsoft.Extensions.Logging;

namespace HopTracer.Core.Services;

/// <summary>
/// Computes the difference between two Grasshopper graphs.
/// </summary>
public class Differ : IDiffer
{
    private readonly ILogger<Differ> _logger;

    public Differ(ILogger<Differ> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Computes the diff between old and new graphs.
    /// Returns a tuple of (diffNodes, diffEdges).
    /// </summary>
    public (List<Node> Nodes, List<Edge> Edges) Diff(Graph oldGraph, Graph newGraph)
    {
        _logger.LogInformation("Starting diff between graphs. Old nodes: {OldCount}, New nodes: {NewCount}", oldGraph.Nodes.Count, newGraph.Nodes.Count);

        // 1. Diff Nodes
        var allIds = oldGraph.Nodes.Keys.Union(newGraph.Nodes.Keys).ToHashSet();
        var diffNodes = new List<Node>();

        foreach (var nid in allIds)
        {
            var inOld = oldGraph.Nodes.ContainsKey(nid);
            var inNew = newGraph.Nodes.ContainsKey(nid);

            if (inOld && inNew)
            {
                // Node exists in both - check for modifications
                var nOld = oldGraph.Nodes[nid];
                var nNew = newGraph.Nodes[nid];

                var dx = nNew.X - nOld.X;
                var dy = nNew.Y - nOld.Y;
                var status = "same";

                var modified = nOld.Name != nNew.Name || nOld.Nickname != nNew.Nickname;

                // Port-level diff
                var mergedInputs = DiffPorts(nOld.Inputs, nNew.Inputs);
                var mergedOutputs = DiffPorts(nOld.Outputs, nNew.Outputs);

                // Property-level diff
                var properties = new Dictionary<string, string>();
                foreach (var kv in nNew.Properties) properties[kv.Key] = kv.Value;
                foreach (var kv in nOld.Properties)
                {
                    if (!properties.ContainsKey(kv.Key)) properties[kv.Key] = kv.Value;
                    if (nNew.Properties.TryGetValue(kv.Key, out var newVal) && kv.Value != newVal)
                    {
                        modified = true;
                    }
                }

                if (mergedInputs.Any(p => p.Status != "same" || p.ValueChanged) ||
                    mergedOutputs.Any(p => p.Status != "same" || p.ValueChanged))
                {
                    modified = true;
                }

                if (modified)
                {
                    status = "modified";
                }

                var nodeOut = new Node
                {
                    Id = nNew.Id,
                    Name = nNew.Name,
                    Nickname = nNew.Nickname,
                    X = nNew.X,
                    Y = nNew.Y,
                    W = nNew.W,
                    H = nNew.H,
                    Status = status,
                    Dx = dx,
                    Dy = dy,
                    Inputs = mergedInputs,
                    Outputs = mergedOutputs,
                    Properties = properties
                };

                diffNodes.Add(nodeOut);
            }
            else if (inNew)
            {
                // Node added
                var n = newGraph.Nodes[nid];
                n.Status = "added";
                diffNodes.Add(n);
            }
            else if (inOld)
            {
                // Node removed
                var n = oldGraph.Nodes[nid];
                n.Status = "removed";
                diffNodes.Add(n);
            }
        }

        // 2. Diff Edges
        var edgeComparer = new EdgeComparer();
        var oldEdges = oldGraph.Edges.ToHashSet(edgeComparer);
        var newEdges = newGraph.Edges.ToHashSet(edgeComparer);
        var allEdges = oldEdges.Union(newEdges, edgeComparer);

        var diffEdges = new List<Edge>();
        var nodeIoStats = new Dictionary<string, (int InAdded, int InRemoved, int OutAdded, int OutRemoved)>();

        foreach (var nid in allIds)
        {
            nodeIoStats[nid] = (0, 0, 0, 0);
        }

        foreach (var edge in allEdges)
        {
            var isOld = oldEdges.Contains(edge);
            var isNew = newEdges.Contains(edge);

            var status = "same";
            
            if (isOld && isNew)
            {
                status = "same";
            }
            else if (isNew)
            {
                status = "added";
                UpdateStats(nodeIoStats, edge.Target, stats => (stats.InAdded + 1, stats.InRemoved, stats.OutAdded, stats.OutRemoved));
                UpdateStats(nodeIoStats, edge.Source, stats => (stats.InAdded, stats.InRemoved, stats.OutAdded + 1, stats.OutRemoved));
            }
            else // isOld
            {
                status = "removed";
                UpdateStats(nodeIoStats, edge.Target, stats => (stats.InAdded, stats.InRemoved + 1, stats.OutAdded, stats.OutRemoved));
                UpdateStats(nodeIoStats, edge.Source, stats => (stats.InAdded, stats.InRemoved, stats.OutAdded, stats.OutRemoved + 1));
            }

            diffEdges.Add(new Edge { Source = edge.Source, Target = edge.Target, SourcePort = edge.SourcePort, TargetPort = edge.TargetPort, Status = status });
        }

        // Update nodes with IO stats
        foreach (var n in diffNodes)
        {
            if (nodeIoStats.TryGetValue(n.Id, out var stats))
            {
                n.InAdded = stats.InAdded;
                n.InRemoved = stats.InRemoved;
                n.OutAdded = stats.OutAdded;
                n.OutRemoved = stats.OutRemoved;

                // Mark as modified if status is 'same' but has IO changes
                if (n.Status == "same" && 
                    (stats.InAdded > 0 || stats.InRemoved > 0 || stats.OutAdded > 0 || stats.OutRemoved > 0))
                {
                    n.Status = "modified";
                }
            }
        }

        _logger.LogInformation("Diff complete. Found {DiffNodeCount} nodes and {DiffEdgeCount} edges.", diffNodes.Count, diffEdges.Count);
        return (diffNodes, diffEdges);
    }

    private List<Port> DiffPorts(List<Port> oldPorts, List<Port> newPorts)
    {
        var result = new List<Port>();
        var allIds = oldPorts.Select(p => p.Id).Union(newPorts.Select(p => p.Id)).ToHashSet();

        foreach (var pid in allIds)
        {
            var inOld = oldPorts.FirstOrDefault(p => p.Id == pid);
            var inNew = newPorts.FirstOrDefault(p => p.Id == pid);

            if (inOld != null && inNew != null)
            {
                var valueChanged = (inOld.Value ?? "") != (inNew.Value ?? "");
                var status = valueChanged || inOld.Name != inNew.Name || inOld.Nickname != inNew.Nickname ? "modified" : "same";

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
                    Status = status
                });
            }
            else if (inNew != null)
            {
                inNew.Status = "added";
                result.Add(inNew);
            }
            else if (inOld != null)
            {
                inOld.Status = "removed";
                result.Add(inOld);
            }
        }

        return result.OrderBy(p => p.Id).ToList();
    }

    private void UpdateStats(Dictionary<string, (int InAdded, int InRemoved, int OutAdded, int OutRemoved)> statsDict, string id, Func<(int InAdded, int InRemoved, int OutAdded, int OutRemoved), (int, int, int, int)> updateFunc)
    {
        if (statsDict.TryGetValue(id, out var currentStats))
        {
            statsDict[id] = updateFunc(currentStats);
        }
    }
}

internal class EdgeComparer : IEqualityComparer<Edge>
{
    public bool Equals(Edge? x, Edge? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (x is null || y is null) return false;
        return x.Source == y.Source && x.SourcePort == y.SourcePort && x.Target == y.Target && x.TargetPort == y.TargetPort;
    }

    public int GetHashCode(Edge obj)
    {
        return HashCode.Combine(obj.Source, obj.SourcePort, obj.Target, obj.TargetPort);
    }
}
