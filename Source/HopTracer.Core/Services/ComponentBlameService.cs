using HopTracer.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace HopTracer.Core.Services;

public sealed class ComponentBlameEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nickname { get; set; } = string.Empty;
    public string Commit { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    /// <summary>commit | uncommitted | history-truncated</summary>
    public string Origin { get; set; } = "commit";
}

public sealed class ComponentBlameResult
{
    public string File { get; set; } = string.Empty;
    public string? AtCommit { get; set; }
    public int Limit { get; set; }
    public int CommitsScanned { get; set; }
    public bool Truncated { get; set; }
    public List<ComponentBlameEntry> Components { get; set; } = new();
}

/// <summary>
/// Attributes each Grasshopper component to the newest commit that added or changed it.
/// </summary>
public sealed class ComponentBlameService
{
    private readonly IGhxParser _parser;
    private readonly IConverterService _converter;

    public ComponentBlameService(IGhxParser? parser = null, IConverterService? converter = null)
    {
        _parser = parser ?? new GhxParser(NullLogger<GhxParser>.Instance);
        _converter = converter ?? new ConverterService(NullLogger<ConverterService>.Instance);
    }

    public ComponentBlameResult Blame(
        IGitWrapper git,
        string filePath,
        int limit = 50,
        string? atCommit = null,
        CancellationToken cancellationToken = default)
    {
        // limit == 0 means walk the full file history (Time Machine / full blame).
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "limit must be >= 0");
        }

        var fullPath = Path.GetFullPath(filePath);
        var temps = new List<string>();
        var unlimited = limit == 0;

        try
        {
            // When anchoring to a commit, pull a wider window so the tip is still findable, then trim.
            var historyWindow = unlimited
                ? 0
                : (string.IsNullOrWhiteSpace(atCommit) ? limit : Math.Max(limit * 4, 100));
            var history = git.GetCommits(fullPath, historyWindow);
            cancellationToken.ThrowIfCancellationRequested();

            Graph current;
            List<CommitInfo> commits;
            CommitInfo prevInfo;
            string prevOrigin;

            if (!string.IsNullOrWhiteSpace(atCommit))
            {
                var tipPath = WriteCommitTemp(git, atCommit, fullPath, temps);
                current = ParseFile(tipPath);

                var tipIndex = FindCommitIndex(history, atCommit);
                if (tipIndex >= 0)
                {
                    var slice = history.Skip(tipIndex);
                    commits = unlimited ? slice.ToList() : slice.Take(limit).ToList();
                    prevInfo = commits[0];
                }
                else
                {
                    // Tip is older than the history window — do not mix in newer HEAD commits.
                    prevInfo = new CommitInfo
                    {
                        Hash = atCommit,
                        Author = "unknown",
                        Date = "",
                        Message = ""
                    };
                    commits = [prevInfo];
                }

                prevOrigin = "commit";
            }
            else
            {
                current = ParseFile(fullPath);
                commits = unlimited ? history.ToList() : history.Take(limit).ToList();
                prevInfo = new CommitInfo
                {
                    Hash = "uncommitted",
                    Author = Environment.UserName,
                    Date = "now",
                    Message = "working tree"
                };
                prevOrigin = "uncommitted";
            }

            var remaining = current.Nodes.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var attributions = new Dictionary<string, ComponentBlameEntry>(StringComparer.OrdinalIgnoreCase);
            var prev = current;

            foreach (var commit in commits)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var olderPath = WriteCommitTemp(git, commit.Hash, fullPath, temps);
                var older = ParseFile(olderPath);

                foreach (var id in remaining.ToList())
                {
                    if (!prev.Nodes.TryGetValue(id, out var prevNode))
                    {
                        continue;
                    }

                    if (!older.Nodes.TryGetValue(id, out var olderNode) ||
                        Fingerprint(olderNode) != Fingerprint(prevNode))
                    {
                        attributions[id] = MakeEntry(prevNode, prevInfo, prevOrigin);
                        remaining.Remove(id);
                    }
                }

                prev = older;
                prevInfo = commit;
                prevOrigin = "commit";
            }

            var tipMissingFromWindow = !string.IsNullOrWhiteSpace(atCommit)
                && FindCommitIndex(history, atCommit) < 0;
            var truncated = (!unlimited && commits.Count >= limit) || tipMissingFromWindow;
            var oldestOrigin = truncated ? "history-truncated" : prevOrigin;
            foreach (var id in remaining)
            {
                if (!prev.Nodes.TryGetValue(id, out var node) && !current.Nodes.TryGetValue(id, out node))
                {
                    continue;
                }

                attributions[id] = MakeEntry(node, prevInfo, oldestOrigin);
            }

            return new ComponentBlameResult
            {
                File = fullPath,
                AtCommit = string.IsNullOrWhiteSpace(atCommit) ? null : atCommit,
                Limit = limit,
                CommitsScanned = commits.Count,
                Truncated = truncated,
                Components = attributions.Values
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };
        }
        finally
        {
            foreach (var temp in temps)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* shortcut: best-effort temp cleanup, ignore lock races */ }
            }
        }
    }

    private static int FindCommitIndex(IReadOnlyList<CommitInfo> commits, string hash)
    {
        for (var i = 0; i < commits.Count; i++)
        {
            if (HashesMatch(commits[i].Hash, hash))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HashesMatch(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
        {
            return false;
        }

        return a.StartsWith(b, StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
    }

    private Graph ParseFile(string path)
    {
        var temps = new List<string>();
        try
        {
            var prepared = PreparePath(path, temps);
            return _parser.Parse(prepared);
        }
        finally
        {
            foreach (var temp in temps)
            {
                try { if (File.Exists(temp)) File.Delete(temp); } catch { /* shortcut: best-effort temp cleanup, ignore lock races */ }
            }
        }
    }

    private string PreparePath(string path, List<string> temps)
    {
        if (!path.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (!_converter.TryCheckDependencies(out var message))
        {
            throw new InvalidOperationException(message);
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"hoptracer-blame-{Guid.NewGuid():N}.ghx");
        temps.Add(tmp);
        return _converter.ConvertGhToGhx(path, tmp);
    }

    private static string WriteCommitTemp(IGitWrapper git, string commitHash, string filePath, List<string> temps)
    {
        var ext = Path.GetExtension(filePath);
        var tmp = Path.Combine(Path.GetTempPath(), $"hoptracer-blame-{Guid.NewGuid():N}{ext}");
        temps.Add(tmp);
        File.WriteAllBytes(tmp, git.GetFileContentAtCommit(commitHash, filePath));
        return tmp;
    }

    private static ComponentBlameEntry MakeEntry(Node node, CommitInfo commit, string origin) => new()
    {
        Id = node.Id,
        Name = node.Name,
        Nickname = node.Nickname,
        Commit = commit.Hash,
        Author = commit.Author,
        Date = commit.Date,
        Message = commit.Message,
        Origin = origin
    };

    internal static string Fingerprint(Node node)
    {
        var props = string.Join('\u001f',
            node.Properties
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));
        var ports = string.Join('\u001f',
            node.Inputs.Concat(node.Outputs)
                .OrderBy(p => p.Id, StringComparer.Ordinal)
                .Select(p => $"{p.Id}:{p.Name}:{p.Value}:{p.WireDisplay}"));
        return $"{node.Name}\u001e{node.Nickname}\u001e{props}\u001e{ports}";
    }
}
