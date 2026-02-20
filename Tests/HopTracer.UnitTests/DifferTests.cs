using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HopTracer.UnitTests;

public class DifferTests
{
    private readonly Differ _differ = new(NullLogger<Differ>.Instance);

    [Fact]
    public void Diff_FlagsPropertyAndConnectionChanges()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node
                {
                    Id = "A",
                    Name = "Slider",
                    Nickname = "OldNick",
                    X = 10,
                    Y = 20,
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:in:0",
                            Name = "Input",
                            Kind = "input",
                            Value = "1"
                        }
                    },
                    Outputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:out:0",
                            Name = "Out",
                            Kind = "output"
                        }
                    },
                    Properties = new Dictionary<string, string> { { "Version", "1" } }
                },
                ["Z"] = new Node
                {
                    Id = "Z",
                    Name = "Sink"
                }
            },
            Edges = new List<Edge>
            {
                new() { Source = "A", SourcePort = "A:out:0", Target = "Z", TargetPort = "Z:in:0", Status = "same" }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node
                {
                    Id = "A",
                    Name = "Slider v2",
                    Nickname = "NewNick",
                    X = 15,
                    Y = 25,
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:in:0",
                            Name = "Input",
                            Kind = "input",
                            Value = "2"
                        }
                    },
                    Outputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:out:0",
                            Name = "Out",
                            Kind = "output"
                        }
                    },
                    Properties = new Dictionary<string, string> { { "Version", "2" } }
                },
                ["B"] = new Node
                {
                    Id = "B",
                    Name = "Preview",
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "B:in:0",
                            Name = "Input",
                            Kind = "input"
                        }
                    },
                    Outputs = new List<Port>()
                }
            },
            Edges = new List<Edge>
            {
                new() { Source = "A", SourcePort = "A:out:0", Target = "B", TargetPort = "B:in:0", Status = "same" }
            }
        };

        var (nodes, edges) = _differ.Diff(oldGraph, newGraph);

        var nodeA = nodes.Single(n => n.Id == "A");
        Assert.Equal("modified", nodeA.Status);
        Assert.Equal("1", nodeA.PropertiesOld?["Version"]);
        Assert.Equal(1, nodeA.OutAdded);
        Assert.Equal(1, nodeA.OutRemoved);
        Assert.Equal("NewNick", nodeA.Nickname);

        var input = nodeA.Inputs.Single(p => p.Id == "A:in:0");
        Assert.True(input.ValueChanged);
        Assert.Equal("1", input.ValueOld);
        Assert.Equal("2", input.ValueNew);

        var nodeB = nodes.Single(n => n.Id == "B");
        Assert.Equal("added", nodeB.Status);
        Assert.Equal(1, nodeB.InAdded);

        var nodeZ = nodes.Single(n => n.Id == "Z");
        Assert.Equal("removed", nodeZ.Status);
        Assert.Equal(1, nodeZ.InRemoved);

        Assert.Contains(edges, e => e.Status == "added" && e.Target == "B" && e.Source == "A");
        Assert.Contains(edges, e => e.Status == "removed" && e.Target == "Z" && e.Source == "A");
    }

    [Fact]
    public void Diff_DetectsClusterChanges()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["C"] = new Node
                {
                    Id = "C",
                    Name = "Cluster",
                    Properties = new Dictionary<string, string> 
                    { 
                        { "IsCluster", "true" },
                        { "ClusterHash", "ABCDEF123456" },
                        { "ClusterSize", "1024" }
                    }
                }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["C"] = new Node
                {
                    Id = "C",
                    Name = "Cluster",
                    Properties = new Dictionary<string, string> 
                    { 
                        { "IsCluster", "true" },
                        { "ClusterHash", "FEDCBA654321" }, // Changed hash
                        { "ClusterSize", "1100" }
                    }
                }
            }
        };

        var (nodes, _) = _differ.Diff(oldGraph, newGraph);
        var nodeC = nodes.Single(n => n.Id == "C");

        Assert.Equal("modified", nodeC.Status);
        Assert.Equal("ABCDEF123456", nodeC.PropertiesOld?["ClusterHash"]);
        Assert.Equal("1024", nodeC.PropertiesOld?["ClusterSize"]);
        Assert.Equal("FEDCBA654321", nodeC.Properties["ClusterHash"]);
    }

    [Fact]
    public void DiffDetailed_MatchesStableComponentsWhenIdsChange()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["old_a"] = new Node
                {
                    Id = "old_a",
                    Name = "Number Slider",
                    Nickname = "A",
                    X = 100,
                    Y = 100,
                    Outputs = new List<Port> { new() { Id = "old_a:out:0", Name = "Value", Kind = "output" } }
                },
                ["old_b"] = new Node
                {
                    Id = "old_b",
                    Name = "Panel",
                    Nickname = "B",
                    X = 250,
                    Y = 100,
                    Inputs = new List<Port> { new() { Id = "old_b:in:0", Name = "Text", Kind = "input" } }
                }
            },
            Edges = new List<Edge>
            {
                new() { Source = "old_a", SourcePort = "old_a:out:0", Target = "old_b", TargetPort = "old_b:in:0", Status = "same" }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["new_a"] = new Node
                {
                    Id = "new_a",
                    Name = "Number Slider",
                    Nickname = "A",
                    X = 102,
                    Y = 100,
                    Outputs = new List<Port> { new() { Id = "new_a:out:0", Name = "Value", Kind = "output" } }
                },
                ["new_b"] = new Node
                {
                    Id = "new_b",
                    Name = "Panel",
                    Nickname = "B",
                    X = 252,
                    Y = 100,
                    Inputs = new List<Port> { new() { Id = "new_b:in:0", Name = "Text", Kind = "input" } }
                }
            },
            Edges = new List<Edge>
            {
                new() { Source = "new_a", SourcePort = "new_a:out:0", Target = "new_b", TargetPort = "new_b:in:0", Status = "same" }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);

        Assert.Equal(2, detailed.Nodes.Count);
        Assert.DoesNotContain(detailed.Nodes, n => n.Status == "added");
        Assert.DoesNotContain(detailed.Nodes, n => n.Status == "removed");
        Assert.Single(detailed.Edges);
        Assert.Equal("same", detailed.Edges[0].Status);
        Assert.Contains(detailed.Diagnostics, d => d.Code == "NODE_IDENTITY_FALLBACK");
    }
}
