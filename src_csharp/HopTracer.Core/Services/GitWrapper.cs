using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace HopTracer.Core.Services;

/// <summary>
/// Wrapper for Git operations.
/// </summary>
public class GitWrapper
{
    private readonly string _repoPath;
    private readonly ILogger<GitWrapper>? _logger;

    public GitWrapper(string repoPath, ILogger<GitWrapper>? logger = null)
    {
        _repoPath = repoPath;
        _logger = logger;
    }

    /// <summary>
    /// Runs a git command and returns the output as string.
    /// </summary>
    private string RunGit(params string[] args)
    {
        using var stream = RunGitStream(args);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    /// <summary>
    /// Runs a git command and returns the output stream.
    /// </summary>
    private Stream RunGitStream(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = _repoPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        var process = Process.Start(psi);
        if (process == null)
        {
            _logger?.LogError("Failed to start git process");
            throw new InvalidOperationException("Failed to start git process");
        }

        // We need to read stderr to check for errors, but we can't block stdout reading.
        // For simple commands, we can wait for exit. For streaming, it's trickier.
        // Here we'll read to memory for simplicity as we don't expect huge outputs for log/show.
        
        var ms = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(ms);
        ms.Position = 0;
        
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            _logger?.LogError("Git error: {Error}", error);
            throw new InvalidOperationException($"Git error: {error}");
        }

        return ms;
    }

    /// <summary>
    /// Checks if the path is inside a git repository.
    /// </summary>
    public bool IsGitRepo()
    {
        try
        {
            RunGit("rev-parse", "--is-inside-work-tree");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the last N commits for a specific file.
    /// </summary>
    public List<CommitInfo> GetCommits(string filePath, int limit = 10)
    {
        try
        {
            var relPath = Path.GetRelativePath(_repoPath, filePath);
            var format = "%h|%an|%ar|%s";
            var output = RunGit("log", $"-n {limit}", $"--pretty=format:{format}", "--", relPath);

            var commits = new List<CommitInfo>();
            if (string.IsNullOrEmpty(output))
            {
                return commits;
            }

            foreach (var line in output.Split('\n'))
            {
                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    commits.Add(new CommitInfo
                    {
                        Hash = parts[0],
                        Author = parts[1],
                        Date = parts[2],
                        Message = parts[3]
                    });
                }
            }

            return commits;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get commits for {FilePath}", filePath);
            return new List<CommitInfo>();
        }
    }

    /// <summary>
    /// Retrieves the content of a file at a specific commit.
    /// </summary>
    public byte[] GetFileContentAtCommit(string commitHash, string filePath)
    {
        var relPath = Path.GetRelativePath(_repoPath, filePath).Replace("\\", "/");
        
        using var stream = RunGitStream("show", $"{commitHash}:{relPath}");
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}

/// <summary>
/// Represents commit information.
/// </summary>
public class CommitInfo
{
    public string Hash { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
