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
    public void Diff_DetectsInputOptionChanges()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node
                {
                    Id = "A",
                    Name = "Panel",
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:in:0",
                            Name = "Input",
                            Kind = "input",
                            Options = new Dictionary<string, string>
                            {
                                ["Flatten"] = "true"
                            }
                        }
                    }
                }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node
                {
                    Id = "A",
                    Name = "Panel",
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:in:0",
                            Name = "Input",
                            Kind = "input",
                            Options = new Dictionary<string, string>
                            {
                                ["Reverse"] = "true"
                            }
                        }
                    }
                }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var node = detailed.Nodes.Single(n => n.Id == "A");
        var input = Assert.Single(node.Inputs);

        Assert.Equal("modified", node.Status);
        Assert.Equal("modified", input.Status);
        Assert.True(input.OptionsChanged);
        Assert.Equal("true", input.Options["Reverse"]);
        Assert.Equal("true", input.OptionsOld?["Flatten"]);
        Assert.Equal("false", input.OptionsNew?["Flatten"]);
        Assert.Equal("false", input.OptionsOld?["Reverse"]);
        Assert.Equal("true", input.OptionsNew?["Reverse"]);
    }

    [Fact]
    public void Diff_DoesNotFlagMissingAndExplicitFalseInputOptionsAsChanges()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node
                {
                    Id = "A",
                    Name = "Panel",
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:in:0",
                            Name = "Input",
                            Kind = "input",
                            Options = new Dictionary<string, string>()
                        }
                    }
                }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node
                {
                    Id = "A",
                    Name = "Panel",
                    Inputs = new List<Port>
                    {
                        new()
                        {
                            Id = "A:in:0",
                            Name = "Input",
                            Kind = "input",
                            Options = new Dictionary<string, string>
                            {
                                ["Flatten"] = "false"
                            }
                        }
                    }
                }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var node = detailed.Nodes.Single(n => n.Id == "A");
        var input = Assert.Single(node.Inputs);

        Assert.Equal("same", node.Status);
        Assert.Equal("same", input.Status);
        Assert.False(input.OptionsChanged);
        Assert.Null(input.OptionsOld);
        Assert.Null(input.OptionsNew);
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

    [Fact]
    public void DiffDetailed_ConnectionOnlyChangesStillReceiveRisk()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Source", Outputs = new List<Port> { new() { Id = "A:out:0", Name = "Out", Kind = "output" } } },
                ["B"] = new() { Id = "B", Name = "Panel", Inputs = new List<Port> { new() { Id = "B:in:0", Name = "In", Kind = "input" } } },
                ["C"] = new() { Id = "C", Name = "Panel", Inputs = new List<Port> { new() { Id = "C:in:0", Name = "In", Kind = "input" } } }
            },
            Edges = new List<Edge>
            {
                new() { Source = "A", SourcePort = "A:out:0", Target = "B", TargetPort = "B:in:0", Status = "same" }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Source", Outputs = new List<Port> { new() { Id = "A:out:0", Name = "Out", Kind = "output" } } },
                ["B"] = new() { Id = "B", Name = "Panel", Inputs = new List<Port> { new() { Id = "B:in:0", Name = "In", Kind = "input" } } },
                ["C"] = new() { Id = "C", Name = "Panel", Inputs = new List<Port> { new() { Id = "C:in:0", Name = "In", Kind = "input" } } }
            },
            Edges = new List<Edge>
            {
                new() { Source = "A", SourcePort = "A:out:0", Target = "C", TargetPort = "C:in:0", Status = "same" }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var nodes = detailed.Nodes.Where(n => n.Id is "A" or "B" or "C").ToDictionary(n => n.Id, StringComparer.Ordinal);

        foreach (var id in new[] { "A", "B", "C" })
        {
            Assert.Equal("modified", nodes[id].Status);
            Assert.Equal(15, nodes[id].RiskScore);
            Assert.Contains("Connection changes", nodes[id].RiskReasons);
        }
    }

    [Fact]
    public void DiffDetailed_SmallMovementBelowTolerance_IsRoundedOut()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Slider", X = 100.00, Y = 200.00 }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Slider", X = 100.04, Y = 200.04 }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var node = detailed.Nodes.Single(n => n.Id == "A");

        Assert.Equal("same", node.Status);
        Assert.Equal(0, node.RiskScore);
        Assert.Equal(0, node.Dx);
        Assert.Equal(0, node.Dy);
        Assert.DoesNotContain("Component moved", node.RiskReasons);
    }

    [Fact]
    public void DiffDetailed_ModifiedNodesAlwaysHaveRiskScore()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Panel", Properties = new Dictionary<string, string> { ["Value"] = "1" } },
                ["B"] = new() { Id = "B", Name = "Relay", Inputs = new List<Port> { new() { Id = "B:in:0", Name = "In", Kind = "input" } } }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Panel", Properties = new Dictionary<string, string> { ["Value"] = "2" } },
                ["B"] = new() { Id = "B", Name = "Relay", Inputs = new List<Port> { new() { Id = "B:in:0", Name = "In", Kind = "input" } } }
            },
            Edges = new List<Edge>
            {
                new() { Source = "A", SourcePort = "A:out:0", Target = "B", TargetPort = "B:in:0", Status = "same" }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var modifiedNodes = detailed.Nodes.Where(n => n.Status == "modified").ToList();

        Assert.NotEmpty(modifiedNodes);
        Assert.All(modifiedNodes, node => Assert.True(node.RiskScore > 0));
    }

    [Fact]
    public void DiffDetailed_DoesNotFlagConnectionChanges_ForBraceOrCaseOnlyEdgeIdDifferences()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Source", Outputs = new List<Port> { new() { Id = "out-guid", Name = "Out", Kind = "output" } } },
                ["B"] = new() { Id = "B", Name = "Merge", Inputs = new List<Port> { new() { Id = "in-guid", Name = "In", Kind = "input" } } }
            },
            Edges = new List<Edge>
            {
                new() { Source = "{A}", SourcePort = "{OUT-GUID}", Target = "{B}", TargetPort = "{IN-GUID}", Status = "same" }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new() { Id = "A", Name = "Source", Outputs = new List<Port> { new() { Id = "out-guid", Name = "Out", Kind = "output" } } },
                ["B"] = new() { Id = "B", Name = "Merge", Inputs = new List<Port> { new() { Id = "in-guid", Name = "In", Kind = "input" } } }
            },
            Edges = new List<Edge>
            {
                new() { Source = "a", SourcePort = "out-guid", Target = "b", TargetPort = "in-guid", Status = "same" }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var nodeA = detailed.Nodes.Single(n => n.Id == "A");
        var nodeB = detailed.Nodes.Single(n => n.Id == "B");

        Assert.Equal("same", nodeA.Status);
        Assert.Equal("same", nodeB.Status);
        Assert.Equal(0, nodeA.RiskScore);
        Assert.Equal(0, nodeB.RiskScore);
        Assert.All(detailed.Edges, e => Assert.Equal("same", e.Status));
    }

    [Fact]
    public void DiffDetailed_DetectsScriptChange_WhenKeyCasingDiffers()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["S"] = new()
                {
                    Id = "S",
                    Name = "C# Script",
                    Properties = new Dictionary<string, string>
                    {
                        ["scriptsource"] = "int x = 1;"
                    }
                }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["S"] = new()
                {
                    Id = "S",
                    Name = "C# Script",
                    Properties = new Dictionary<string, string>
                    {
                        ["ScriptSource"] = "int x = 2;"
                    }
                }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var node = detailed.Nodes.Single(n => n.Id == "S");

        Assert.Equal("modified", node.Status);
        Assert.NotNull(node.PropertiesOld);
        Assert.Equal("int x = 1;", node.PropertiesOld!["ScriptSource"]);
        Assert.Equal("int x = 2;", node.Properties["ScriptSource"]);
    }

    [Fact]
    public void DiffDetailed_CapturesEmptyOldValue_ForNewScriptPropertyOnModifiedNode()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["S"] = new()
                {
                    Id = "S",
                    Name = "Python Script",
                    X = 0,
                    Y = 0,
                    Properties = new Dictionary<string, string>()
                }
            }
        };

        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["S"] = new()
                {
                    Id = "S",
                    Name = "Python Script",
                    X = 10,
                    Y = 0,
                    Properties = new Dictionary<string, string>
                    {
                        ["ScriptSource"] = "x = 1\nprint(x)"
                    }
                }
            }
        };

        var detailed = _differ.DiffDetailed(oldGraph, newGraph);
        var node = detailed.Nodes.Single(n => n.Id == "S");

        Assert.Equal("modified", node.Status);
        Assert.NotNull(node.PropertiesOld);
        Assert.Equal(string.Empty, node.PropertiesOld!["ScriptSource"]);
        Assert.Equal("x = 1\nprint(x)", node.Properties["ScriptSource"]);
    }
}
