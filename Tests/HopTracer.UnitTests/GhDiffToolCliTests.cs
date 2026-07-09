using GhDiffTool;
using HopTracer.Core.Services;
using System.Text.Json;
using Xunit;

namespace HopTracer.UnitTests;

public class GhDiffToolCliTests
{
    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")) &&
                    Directory.Exists(Path.Combine(dir.FullName, "Tests", "data")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate repository root.");
        }
    }

    private static string FixturePath(string name) => Path.Combine(RepoRoot, "Tests", "data", name);

    [Fact]
    public void Help_ReturnsZero()
    {
        var stdout = new StringWriter();
        var code = CliRunner.Run(["help"], stdout);
        Assert.Equal(0, code);
        Assert.Contains("hoptracer compare", stdout.ToString());
    }

    [Fact]
    public void Version_ReturnsZeroAndVersionString()
    {
        var stdout = new StringWriter();
        var code = CliRunner.Run(["--version"], stdout);
        Assert.Equal(0, code);
        Assert.StartsWith("hoptracer 1.", stdout.ToString());
    }

    [Fact]
    public void Compare_Fixtures_AgentFormat_ReturnsZero()
    {
        var oldPath = FixturePath("compare-old.ghx");
        var newPath = FixturePath("compare-new.ghx");
        var stdout = new StringWriter();

        var code = CliRunner.Run(["compare", oldPath, newPath, "--format", "agent"], stdout);

        Assert.Equal(0, code);
        using var doc = JsonDocument.Parse(stdout.ToString());
        Assert.True(doc.RootElement.GetProperty("totalChangedNodes").GetInt32() >= 1);
        Assert.Contains("added", doc.RootElement.GetProperty("summary").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_MissingFile_AgentFormat_EmitsStructuredError()
    {
        var stderr = new StringWriter();
        var code = CliRunner.Run(
            ["compare", FixturePath("missing-old.ghx"), FixturePath("compare-new.ghx"), "--format", "agent"],
            stderr: stderr);

        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(stderr.ToString().Trim());
        Assert.Equal("file_not_found", doc.RootElement.GetProperty("error").GetString());
        Assert.True(doc.RootElement.TryGetProperty("path", out _));
    }

    [Fact]
    public void Compare_MissingFile_TextFormat_EmitsPlainError()
    {
        var stderr = new StringWriter();
        var code = CliRunner.Run(
            ["compare", FixturePath("missing-old.ghx"), FixturePath("compare-new.ghx")],
            stderr: stderr);

        Assert.Equal(1, code);
        Assert.StartsWith("File not found:", stderr.ToString().Trim());
        Assert.DoesNotContain("\"error\"", stderr.ToString());
    }

    [Fact]
    public void Compare_FailOnRisk_ReturnsOneWhenHighRisk()
    {
        var oldPath = FixturePath("compare-old.ghx");
        var newPath = FixturePath("compare-new.ghx");

        var code = CliRunner.Run(["compare", oldPath, newPath, "--format", "agent", "--fail-on-risk"]);

        Assert.Equal(1, code);
    }

    [Fact]
    public void UnknownCommand_ReturnsOne()
    {
        var stderr = new StringWriter();
        var code = CliRunner.Run(["nope"], stderr: stderr);
        Assert.Equal(1, code);
        Assert.Contains("Unknown command", stderr.ToString());
    }

    [Fact]
    public void Compare_UnknownOption_AgentFormat_StructuredError()
    {
        var stderr = new StringWriter();
        var code = CliRunner.Run(
            ["compare", FixturePath("compare-old.ghx"), FixturePath("compare-new.ghx"), "--format", "agent", "--nope"],
            stderr: stderr);

        Assert.Equal(1, code);
        using var doc = JsonDocument.Parse(stderr.ToString().Trim());
        Assert.Equal("error", doc.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void ParseArgs_RecognizesFormatAndFlags()
    {
        var (flags, positional) = CliRunner.ParseArgsForTests(
            ["old.ghx", "new.ghx", "--format", "agent", "--fail-on-risk", "--max-nodes", "5"]);

        Assert.Equal(new[] { "old.ghx", "new.ghx" }, positional);
        Assert.Equal(DiffOutputFormat.Agent, flags.Format);
        Assert.True(flags.FailOnRisk);
        Assert.Equal(5, flags.MaxNodes);
    }
}
