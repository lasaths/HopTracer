using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HopTracer.UnitTests;

public class DiffOutputGeneratorTests
{
    private readonly DiffOutputGenerator _generator = new();
    private readonly Differ _differ = new(NullLogger<Differ>.Instance);

    [Fact]
    public void Generate_Text_IncludesRiskSummary()
    {
        var diff = _differ.DiffDetailed(CreateOldGraph(), CreateNewGraph());
        var text = _generator.Generate(diff, new DiffOutputOptions
        {
            FileOld = "old.ghx",
            FileNew = "new.ghx"
        }, DiffOutputFormat.Text);

        Assert.Contains("old.ghx", text);
        Assert.Contains("new.ghx", text);
        Assert.Contains("RISK SUMMARY", text);
        Assert.Contains("STATISTICS", text);
    }

    [Fact]
    public void Generate_Json_RespectsIncludeChangedNodesFlag()
    {
        var diff = _differ.DiffDetailed(CreateOldGraph(), CreateNewGraph());

        var sparse = _generator.Generate(diff, new DiffOutputOptions { IncludeChangedNodes = false }, DiffOutputFormat.Json);
        Assert.DoesNotContain("\"changedNodes\"", sparse);

        var full = _generator.Generate(diff, new DiffOutputOptions { IncludeChangedNodes = true }, DiffOutputFormat.Json);
        Assert.Contains("\"changedNodes\"", full);
    }

    [Fact]
    public void Generate_Html_EncodesScriptInNodeName()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node { Id = "A", Name = "Safe" }
            }
        };
        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["A"] = new Node { Id = "A", Name = "Safe", Nickname = "<script>alert(1)</script>" }
            }
        };

        var diff = _differ.DiffDetailed(oldGraph, newGraph);
        var html = _generator.Generate(diff, new DiffOutputOptions(), DiffOutputFormat.Html);

        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
    }

    private static Graph CreateOldGraph() => new()
    {
        Nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Name = "Slider", X = 10, Y = 20 }
        }
    };

    private static Graph CreateNewGraph() => new()
    {
        Nodes = new Dictionary<string, Node>
        {
            ["A"] = new Node { Id = "A", Name = "Slider v2", X = 15, Y = 25 }
        }
    };
}
