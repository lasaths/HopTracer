using HopTracer.Core.Services;
using Xunit;
using System.Linq;
using System.Text.Json;

namespace HopTracer.UnitTests;

public class NestedClusterTests
{
    [Fact]
    public void NestedCluster_ProducesCorrectPaths()
    {
        // Arrange
        var parser = new GhxParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<GhxParser>.Instance);
        var differ = new Differ(Microsoft.Extensions.Logging.Abstractions.NullLogger<Differ>.Instance);
        var baseDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "../../../../.."));
        var oldPath = System.IO.Path.Combine(baseDir, "Tests/data/nested-cluster-old.ghx");
        var newPath = System.IO.Path.Combine(baseDir, "Tests/data/nested-cluster-new.ghx");

        // Act
        var oldGraph = parser.Parse(oldPath);
        var newGraph = parser.Parse(newPath);
        var diff = differ.DiffDetailed(oldGraph, newGraph);

        var output = new DiffOutputGenerator();
        var options = new DiffOutputOptions { FileOld = oldPath, FileNew = newPath, IncludeTimestamp = false };
        var json = output.GenerateAgentDiff(diff, options);
        var doc = JsonDocument.Parse(json);

        // Assert
        var changes = doc.RootElement.GetProperty("changes").EnumerateArray().ToList();
        
        // Should have OuterCluster and inner nodes
        Assert.True(changes.Count > 1, $"Expected multiple changes, got {changes.Count}");

        // Find nodes by their names
        var outerCluster = changes.FirstOrDefault(c => c.GetProperty("name").GetString() == "OuterCluster");
        Assert.False(outerCluster.Equals(default(JsonElement)), "OuterCluster not found");
        
        var outerPath = outerCluster.GetProperty("path").EnumerateArray().Select(p => p.GetString()).ToArray();
        Assert.Equal(new[] { "root" }, outerPath);

        // Inner cluster should have path ["root", "OuterCluster"]
        var innerCluster = changes.FirstOrDefault(c => c.GetProperty("name").GetString() == "InnerCluster");
        if (!innerCluster.Equals(default(JsonElement)))
        {
            var innerPath = innerCluster.GetProperty("path").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Equal(new[] { "root", "OuterCluster" }, innerPath);
        }

        // Deep nodes should have path ["root", "OuterCluster", "InnerCluster"]
        var deepNode = changes.FirstOrDefault(c => 
            c.GetProperty("name").GetString() == "DeepModified" ||
            c.GetProperty("name").GetString() == "DeepMult");
        
        if (!deepNode.Equals(default(JsonElement)))
        {
            var deepPath = deepNode.GetProperty("path").EnumerateArray().Select(p => p.GetString()).ToArray();
            Assert.Equal(new[] { "root", "OuterCluster", "InnerCluster" }, deepPath);
        }
    }
}
