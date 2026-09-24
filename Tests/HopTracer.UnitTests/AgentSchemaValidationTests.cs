using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace HopTracer.UnitTests;

public class AgentSchemaValidationTests
{
    private static readonly DiffOutputGenerator Generator = new();
    private static readonly Differ Differ = new(NullLogger<Differ>.Instance);
    private static readonly GhxParser Parser = new(NullLogger<GhxParser>.Instance);

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "skills", "hoptracer", "references", "agent-schema.json")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }

    [Fact]
    public void AgentOutput_FromFixtures_MatchesDocumentedContract()
    {
        var oldPath = Path.Combine(RepoRoot, "Tests", "data", "compare-old.ghx");
        var newPath = Path.Combine(RepoRoot, "Tests", "data", "compare-new.ghx");
        var diff = Differ.DiffDetailed(Parser.Parse(oldPath), Parser.Parse(newPath));
        var json = Generator.Generate(diff, new DiffOutputOptions
        {
            FileOld = oldPath,
            FileNew = newPath
        }, DiffOutputFormat.Agent);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        AssertAgentContract(root);
        Assert.True(root.GetProperty("totalChangedNodes").GetInt32() >= 1);
    }

    [Fact]
    public void AgentSchemaFile_ExistsAndListsRequiredFields()
    {
        var schemaPath = Path.Combine(RepoRoot, "skills", "hoptracer", "references", "agent-schema.json");
        Assert.True(File.Exists(schemaPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var required = doc.RootElement.GetProperty("required");
        Assert.Contains("summary", required.EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("nodeChanges", required.EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("truncatedNodes", required.EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void AgentOutput_IncludesNodeIdAndTruncationMetadata()
    {
        var diff = Differ.DiffDetailed(CreateOldGraph(), CreateNewGraph());
        var json = Generator.Generate(diff, new DiffOutputOptions(), DiffOutputFormat.Agent);
        using var doc = JsonDocument.Parse(json);

        var root = doc.RootElement;
        AssertAgentContract(root);
        Assert.True(root.TryGetProperty("totalChangedNodes", out _));
        Assert.True(root.TryGetProperty("totalChangedEdges", out _));

        var nodeChange = root.GetProperty("nodeChanges")[0];
        Assert.False(string.IsNullOrWhiteSpace(nodeChange.GetProperty("nodeId").GetString()));
    }

    [Fact]
    public void AgentOutput_PropertyChange_IncludesNodeIdWhenPresent()
    {
        var oldGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["P"] = CreateNode("P", "Panel", "Pnl", properties: new Dictionary<string, string>
                {
                    ["PanelContent"] = "old"
                })
            }
        };
        var newGraph = new Graph
        {
            Nodes = new Dictionary<string, Node>
            {
                ["P"] = CreateNode("P", "Panel", "Pnl", properties: new Dictionary<string, string>
                {
                    ["PanelContent"] = "new"
                })
            }
        };

        var diff = Differ.DiffDetailed(oldGraph, newGraph);
        var json = Generator.Generate(diff, new DiffOutputOptions { MaxNodesToShow = 10 }, DiffOutputFormat.Agent);
        using var doc = JsonDocument.Parse(json);

        var change = doc.RootElement.GetProperty("propertyChanges")[0];
        Assert.Equal("P", change.GetProperty("nodeId").GetString());
    }

    private static void AssertAgentContract(JsonElement root)
    {
        foreach (var name in new[]
        {
            "generatedAt", "summary", "statistics", "riskSummary", "topRisks", "wireChanges",
            "propertyChanges", "opaqueChanges", "nodeChanges", "diagnostics",
            "totalChangedNodes", "totalChangedEdges", "truncatedNodes", "truncatedEdges"
        })
        {
            Assert.True(root.TryGetProperty(name, out _), $"Missing agent field: {name}");
        }

        Assert.Equal(JsonValueKind.Array, root.GetProperty("wireChanges").ValueKind);
        Assert.Equal(JsonValueKind.Array, root.GetProperty("nodeChanges").ValueKind);
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
        Dictionary<string, string>? properties = null) => new()
    {
        Id = id,
        Name = name,
        Nickname = nickname,
        Properties = properties ?? new Dictionary<string, string>()
    };
}
