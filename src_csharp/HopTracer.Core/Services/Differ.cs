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
                
                // Mark as modified if name/nickname changed
                if (nOld.Name != nNew.Name || nOld.Nickname != nNew.Nickname)
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
                    Dy = dy
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
        var oldEdges = oldGraph.Edges.ToHashSet();
        var newEdges = newGraph.Edges.ToHashSet();
        var allEdges = oldEdges.Union(newEdges);

        var diffEdges = new List<Edge>();
        var nodeIoStats = new Dictionary<string, (int InAdded, int InRemoved, int OutAdded, int OutRemoved)>();

        foreach (var nid in allIds)
        {
            nodeIoStats[nid] = (0, 0, 0, 0);
        }

        foreach (var (src, tgt) in allEdges)
        {
            var isOld = oldEdges.Contains((src, tgt));
            var isNew = newEdges.Contains((src, tgt));

            var status = "same";
            
            if (isOld && isNew)
            {
                status = "same";
            }
            else if (isNew)
            {
                status = "added";
                UpdateStats(nodeIoStats, tgt, stats => (stats.InAdded + 1, stats.InRemoved, stats.OutAdded, stats.OutRemoved));
                UpdateStats(nodeIoStats, src, stats => (stats.InAdded, stats.InRemoved, stats.OutAdded + 1, stats.OutRemoved));
            }
            else // isOld
            {
                status = "removed";
                UpdateStats(nodeIoStats, tgt, stats => (stats.InAdded, stats.InRemoved + 1, stats.OutAdded, stats.OutRemoved));
                UpdateStats(nodeIoStats, src, stats => (stats.InAdded, stats.InRemoved, stats.OutAdded, stats.OutRemoved + 1));
            }

            diffEdges.Add(new Edge { Source = src, Target = tgt, Status = status });
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

    private void UpdateStats(Dictionary<string, (int InAdded, int InRemoved, int OutAdded, int OutRemoved)> statsDict, string id, Func<(int InAdded, int InRemoved, int OutAdded, int OutRemoved), (int, int, int, int)> updateFunc)
    {
        if (statsDict.TryGetValue(id, out var currentStats))
        {
            statsDict[id] = updateFunc(currentStats);
        }
    }
}
