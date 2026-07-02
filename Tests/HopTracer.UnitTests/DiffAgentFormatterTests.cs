using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace HopTracer.UnitTests;

public class DiffAgentFormatterTests
{
    private readonly DiffOutputGenerator _generator = new();
    private readonly Differ _differ = new(NullLogger<Differ>.Instance);

    [Fact]
    public void Generate_Agent_ResolvesWireLabels()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = CreateNode("A", "Source", "Src", outputs: [CreatePort("A:out:0", "Out", "O")]),
                ["B"] = CreateNode("B", "Sink", "Snk", inputs: [CreatePort("B:in:0", "In", "I")])
            },
            Edges = []
        };
        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = CreateNode("A", "Source", "Src", outputs: [CreatePort("A:out:0", "Out", "O")]),
                ["B"] = CreateNode("B", "Sink", "Snk", inputs: [CreatePort("B:in:0", "In", "I")])
            },
            Edges =
            [
                new Edge
                {
                    Source = "A",
                    SourcePort = "A:out:0",
                    Target = "B",
                    TargetPort = "B:in:0",
                    Status = "added"
                }
            ]
        };

        var diff = _differ.DiffDetailed(oldGraph, newGraph);
        var json = _generator.Generate(diff, new DiffOutputOptions(), DiffOutputFormat.Agent);
        using var doc = JsonDocument.Parse(json);

        var wire = doc.RootElement.GetProperty("wireChanges")[0];
        Assert.Equal("Src.O → Snk.I", wire.GetProperty("wire").GetString());
        Assert.Contains("summary", json);
    }

    [Fact]
    public void Generate_Agent_ClassifiesPanelAndOpaqueProperties()
    {
        var blobOld = new string('A', 300);
        var blobNew = new string('B', 300);
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["P"] = CreateNode("P", "Panel", "", properties: new Dictionary<string, string>
                {
                    ["PanelContent"] = "old notes"
                }),
                ["G"] = CreateNode("G", "Geometry", "Geo", properties: new Dictionary<string, string>
                {
                    ["ON_Data"] = blobOld,
                    ["TypeName"] = "Grasshopper.Kernel.Types.GH_Brep",
                    ["Count"] = "5"
                })
            }
        };
        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["P"] = CreateNode("P", "Panel", "", properties: new Dictionary<string, string>
                {
                    ["PanelContent"] = "new notes",
                    ["TypeName"] = "Grasshopper.Kernel.Special.GH_Panel"
                }),
                ["G"] = CreateNode("G", "Geometry", "Geo", properties: new Dictionary<string, string>
                {
                    ["ON_Data"] = blobNew,
                    ["TypeName"] = "Grasshopper.Kernel.Types.GH_Brep",
                    ["Count"] = "5"
                })
            }
        };

        var diff = _differ.DiffDetailed(oldGraph, newGraph);
        var json = _generator.Generate(diff, new DiffOutputOptions { MaxNodesToShow = 10 }, DiffOutputFormat.Agent);

        Assert.Contains("\"propertyChanges\"", json);
        Assert.Contains("old notes", json);
        Assert.Contains("new notes", json);
        Assert.Contains("\"opaqueChanges\"", json);
        Assert.Contains("ON_Data", json);
        Assert.DoesNotContain(new string('A', 300), json);
    }

    [Fact]
    public void Generate_Json_ShowEdges_IncludesSourceLabel()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = CreateNode("A", "Source", "Src", outputs: [CreatePort("A:out:0", "Out", "O")]),
                ["B"] = CreateNode("B", "Sink", "Snk", inputs: [CreatePort("B:in:0", "In", "I")])
            }
        };
        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = CreateNode("A", "Source", "Src", outputs: [CreatePort("A:out:0", "Out", "O")]),
                ["B"] = CreateNode("B", "Sink", "Snk", inputs: [CreatePort("B:in:0", "In", "I")])
            },
            Edges =
            [
                new Edge
                {
                    Source = "A",
                    SourcePort = "A:out:0",
                    Target = "B",
                    TargetPort = "B:in:0",
                    Status = "added"
                }
            ]
        };

        var diff = _differ.DiffDetailed(oldGraph, newGraph);
        var json = _generator.Generate(diff, new DiffOutputOptions { IncludeChangedEdges = true }, DiffOutputFormat.Json);

        Assert.Contains("\"sourceLabel\":\"Src\"", json.Replace(" ", ""));
    }

    [Fact]
    public void Generate_Agent_Summary_NotEmptyWhenNodesAdded()
    {
        var diff = _differ.DiffDetailed(CreateOldGraph(), CreateNewGraph());
        var json = _generator.Generate(diff, new DiffOutputOptions(), DiffOutputFormat.Agent);
        using var doc = JsonDocument.Parse(json);

        var summary = doc.RootElement.GetProperty("summary").GetString();
        Assert.False(string.IsNullOrWhiteSpace(summary));
        Assert.Contains("added", summary, StringComparison.OrdinalIgnoreCase);
    }

    private static Graph CreateOldGraph() => new()
    {
        Nodes = new Dictionary<string, Node>
        {
            ["A"] = CreateNode("A", "Slider", "Sld")
        }
    };

    private static Graph CreateNewGraph() => new()
    {
        Nodes = new Dictionary<string, Node>
        {
            ["A"] = CreateNode("A", "Slider", "Sld"),
            ["B"] = CreateNode("B", "Panel", "Pnl")
        }
    };

    private static Node CreateNode(
        string id,
        string name,
        string nickname,
        List<Port>? inputs = null,
        List<Port>? outputs = null,
        Dictionary<string, string>? properties = null) => new()
    {
        Id = id,
        Name = name,
        Nickname = nickname,
        Inputs = inputs ?? [],
        Outputs = outputs ?? [],
        Properties = properties ?? new Dictionary<string, string>()
    };

    private static Port CreatePort(string id, string name, string nickname) => new()
    {
        Id = id,
        Name = name,
        Nickname = nickname,
        Kind = id.Contains(":out:", StringComparison.Ordinal) ? "output" : "input"
    };
}
