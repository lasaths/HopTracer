using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace HopTracer.UnitTests;

public class GhxParserTests
{
    [Fact]
    public void Parse_UsesBoundsForNodeSizeAndPosition_WhenBoundsExist()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Tests", "data", "SampleDefinition.ghx");
        var graph = parser.Parse(path);

        Assert.True(graph.Nodes.Count > 0);

        var withBounds = graph.Nodes.Values.Where(n => n.W > 0 && n.H > 0).ToList();
        Assert.True(withBounds.Count > 0);
        Assert.All(withBounds, n =>
        {
            Assert.True(n.W > 0);
            Assert.True(n.H > 0);
        });
    }

    [Fact]
    public void Parse_TracksClusterPreviewDiagnostics_WhenPayloadIsNotArchive()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Tests", "data", "Cluster_Base.ghx");
        var graph = parser.Parse(path);

        var node = Assert.Single(graph.Nodes.Values);
        Assert.True(node.Properties.ContainsKey("IsCluster"));
        Assert.True(node.Properties.ContainsKey("ClusterPreviewStatus"));
        Assert.True(graph.Metadata.ClusterPreviewFailed >= 1);
    }

    [Fact]
    public void Parse_ParsesLegacyParamInputOutputConnections()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Tests", "data", "260218_LCRL_Roof_FacadeInterface [Feb-18 '26, 1637].ghx");
        var graph = parser.Parse(path);

        Assert.True(graph.Nodes.Count > 0);
        Assert.True(graph.Edges.Count > 0);
        Assert.All(graph.Edges, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Source));
            Assert.False(string.IsNullOrWhiteSpace(e.Target));
        });
    }

    [Fact]
    public void Parse_ExtractsGroupMembers_ForGrasshopperGroups()
    {
        var parser = new GhxParser(NullLogger<GhxParser>.Instance);
        var root = FindRepoRoot();
        var path = Path.Combine(root, "Tests", "data", "260218_LCRL_Roof_FacadeInterface [Feb-18 '26, 1637].ghx");
        var graph = parser.Parse(path);

        var groups = graph.Nodes.Values
            .Where(n => n.Properties.TryGetValue("IsGroup", out var isGroup) &&
                        string.Equals(isGroup, "true", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(groups);

        var groupWithMembers = groups.FirstOrDefault(n =>
            n.Properties.TryGetValue("GroupMemberIds", out var raw) &&
            !string.IsNullOrWhiteSpace(raw));

        Assert.NotNull(groupWithMembers);

        var memberJson = groupWithMembers!.Properties["GroupMemberIds"];
        var memberIds = JsonSerializer.Deserialize<List<string>>(memberJson);

        Assert.NotNull(memberIds);
        Assert.NotEmpty(memberIds!);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (File.Exists(Path.Combine(dir, "Source", "HopTracer.sln")))
            {
                return dir;
            }

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root for test data.");
    }
}
