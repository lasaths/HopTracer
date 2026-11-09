using HopTracer.Core.Services;
using Xunit;

namespace HopTracer.UnitTests;

public class ComponentBlameServiceTests
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
    public void Blame_AttributesAddedComponentToNewerCommit()
    {
        var working = FixturePath("compare-new.ghx");
        var oldBytes = File.ReadAllBytes(FixturePath("compare-old.ghx"));
        var newBytes = File.ReadAllBytes(working);

        var git = new FakeGit(
            [
                new CommitInfo { Hash = "HEAD01", Author = "Ada", Date = "1 day ago", Message = "add panel" },
                new CommitInfo { Hash = "OLD001", Author = "Bob", Date = "2 days ago", Message = "initial" }
            ],
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["HEAD01"] = newBytes,
                ["OLD001"] = oldBytes
            });

        var result = new ComponentBlameService().Blame(git, working, limit: 10);

        Assert.Equal(2, result.Components.Count);
        var panel = Assert.Single(result.Components, c => c.Name == "Panel");
        Assert.Equal("HEAD01", panel.Commit);
        Assert.Equal("Ada", panel.Author);
        Assert.Equal("commit", panel.Origin);

        var slider = Assert.Single(result.Components, c => c.Name == "Number Slider");
        Assert.Equal("OLD001", slider.Commit);
        Assert.Equal("Bob", slider.Author);
    }

    [Fact]
    public void Blame_UncommittedAdd_UsesWorkingTreeOrigin()
    {
        var working = FixturePath("compare-new.ghx");
        var oldBytes = File.ReadAllBytes(FixturePath("compare-old.ghx"));

        var git = new FakeGit(
            [new CommitInfo { Hash = "OLD001", Author = "Bob", Date = "2 days ago", Message = "initial" }],
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["OLD001"] = oldBytes
            });

        var result = new ComponentBlameService().Blame(git, working, limit: 10);
        var panel = Assert.Single(result.Components, c => c.Name == "Panel");
        Assert.Equal("uncommitted", panel.Commit);
        Assert.Equal("uncommitted", panel.Origin);
    }

    [Fact]
    public void Blame_AtCommit_IgnoresNewerWorkingTreeComponents()
    {
        var working = FixturePath("compare-new.ghx");
        var oldBytes = File.ReadAllBytes(FixturePath("compare-old.ghx"));
        var newBytes = File.ReadAllBytes(working);

        var git = new FakeGit(
            [
                new CommitInfo { Hash = "HEAD01", Author = "Ada", Date = "1 day ago", Message = "add panel" },
                new CommitInfo { Hash = "OLD001", Author = "Bob", Date = "2 days ago", Message = "initial" }
            ],
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["HEAD01"] = newBytes,
                ["OLD001"] = oldBytes
            });

        var result = new ComponentBlameService().Blame(git, working, limit: 10, atCommit: "OLD001");

        Assert.Equal("OLD001", result.AtCommit);
        Assert.DoesNotContain(result.Components, c => c.Name == "Panel");
        var slider = Assert.Single(result.Components);
        Assert.Equal("Number Slider", slider.Name);
        Assert.Equal("OLD001", slider.Commit);
    }

    private sealed class FakeGit : IGitWrapper
    {
        private readonly List<CommitInfo> _commits;
        private readonly Dictionary<string, byte[]> _content;

        public FakeGit(IReadOnlyList<CommitInfo> commits, Dictionary<string, byte[]> content)
        {
            _commits = commits.ToList();
            _content = content;
        }

        public bool IsGitRepo() => true;

        public List<CommitInfo> GetCommits(string filePath, int limit = 10) =>
            _commits.Take(limit <= 0 ? _commits.Count : limit).ToList();

        public byte[] GetFileContentAtCommit(string commitHash, string filePath) =>
            _content.TryGetValue(commitHash, out var bytes)
                ? bytes
                : throw new InvalidOperationException($"missing {commitHash}");
    }
}
