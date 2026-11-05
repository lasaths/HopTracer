using HopTracer.Core.Services;
using Xunit;
using System.Linq;
using System.Text.Json;

namespace HopTracer.UnitTests;

public class RenameDetectionTests
{
    [Fact]
    public void Rename_NicknameChange_ShowsRenameField()
    {
        // Arrange
        var parser = new GhxParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<GhxParser>.Instance);
        var differ = new Differ(Microsoft.Extensions.Logging.Abstractions.NullLogger<Differ>.Instance);
        var baseDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "../../../../.."));
        var oldPath = System.IO.Path.Combine(baseDir, "Tests/data/rename-old.ghx");
        var newPath = System.IO.Path.Combine(baseDir, "Tests/data/rename-new.ghx");

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
        Assert.Single(changes);
        
        var change = changes[0];
        Assert.True(change.TryGetProperty("rename", out var renameElem));
        Assert.NotEqual(JsonValueKind.Null, renameElem.ValueKind);
        
        Assert.True(renameElem.TryGetProperty("oldNickname", out var oldNick));
        Assert.Equal("Sld", oldNick.GetString());
        
        Assert.True(renameElem.TryGetProperty("newNickname", out var newNick));
        Assert.Equal("Slider_Renamed", newNick.GetString());
        
        // Check that risk reasons include the rename
        var risk = change.GetProperty("risk");
        var reasons = risk.GetProperty("reasons").EnumerateArray().Select(r => r.GetString()).ToList();
        Assert.Contains(reasons, r => r != null && r.Contains("Renamed from"));
    }
}
