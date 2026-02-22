using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace HopTracer.Core.Services;

public interface IGitWrapper
{
    bool IsGitRepo();
    List<CommitInfo> GetCommits(string filePath, int limit = 10);
    byte[] GetFileContentAtCommit(string commitHash, string filePath);
}

/// <summary>
/// Wrapper for Git operations.
/// </summary>
public class GitWrapper : IGitWrapper
{
    private readonly ILogger<GitWrapper>? _logger;
    private readonly string _repoRoot;

    // Default constructor for DI - assumes current directory or configured later
    // Ideally, we should inject a factory or configuration, but for now we'll default to current dir
    // or allow setting it.
    public GitWrapper(ILogger<GitWrapper>? logger = null) : this(Directory.GetCurrentDirectory(), logger)
    {
    }

    public GitWrapper(string repoPath, ILogger<GitWrapper>? logger = null)
    {
        _logger = logger;
        _repoRoot = ResolveRepoRoot(repoPath);
        _logger?.LogDebug("Resolved Git repository root: {RepoRoot}", _repoRoot);
    }

    /// <summary>
    /// Runs a git command and returns the output as string.
    /// </summary>
    private string RunGit(params string[] args)
    {
        using var stream = RunGitStream(_repoRoot, args);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    /// <summary>
    /// Runs a git command and returns the output stream.
    /// </summary>
    private Stream RunGitStream(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDir,
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
    /// Returns commit info along with the path used for git log.
    /// Only returns commits where the file actually existed.
    /// </summary>
    public List<CommitInfo> GetCommits(string filePath, int limit = 10)
    {
        try
        {
            WarnIfOutsideRepo(filePath);
            var relPath = ResolveTrackedPath(filePath);
            
            _logger?.LogInformation("Getting commits: file={RelPath}, repo={Repo}", relPath, _repoRoot);
            
            var format = "%h|%an|%ar|%s";
            // Use --follow to track file renames/moves
            // Request more than needed to account for filtering
            var output = RunGitAt(_repoRoot, "log", "--follow", $"-n {limit * 2}", $"--pretty=format:{format}", "--", relPath);

            var commits = new List<CommitInfo>();
            if (string.IsNullOrEmpty(output))
            {
                return commits;
            }

            foreach (var line in output.Split('\n'))
            {
                if (commits.Count >= limit)
                    break; // We have enough commits
                    
                var parts = line.Split('|');
                if (parts.Length >= 4)
                {
                    var hash = parts[0];
                    
                    // Verify the file exists in this commit and capture byte size.
                    try
                    {
                        var sizeRaw = RunGitAt(_repoRoot, "cat-file", "-s", $"{hash}:{relPath}");
                        long.TryParse(sizeRaw, out var fileSizeBytes);
                        
                        // File exists, add the commit.
                        commits.Add(new CommitInfo
                        {
                            Hash = hash,
                            Author = parts[1],
                            Date = parts[2],
                            Message = parts[3],
                            FilePath = relPath, // Store the path we used for the query
                            FileSizeBytes = Math.Max(0, fileSizeBytes)
                        });
                    }
                    catch
                    {
                        // File doesn't exist in this commit, skip it
                        _logger?.LogDebug("Skipping commit {Hash} - file doesn't exist", hash);
                        continue;
                    }
                }
            }

            _logger?.LogInformation("Found {Count} commits where file exists (repo={Repo})", commits.Count, _repoRoot);
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
        try
        {
            WarnIfOutsideRepo(filePath);
            var relPath = ResolveTrackedPath(filePath);
            
            _logger?.LogInformation("Getting file content: commit={Commit}, file={RelPath}, repo={Repo}, fullPath={FullPath}", 
                commitHash, relPath, _repoRoot, Path.GetFullPath(filePath));
            
            // Try to get the file content
            try
            {
                _logger?.LogInformation("Executing: git show {Commit}:{Path}", commitHash, relPath);
                using var stream = RunGitStream(_repoRoot, "show", $"{commitHash}:{relPath}");
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                var bytes = ms.ToArray();
                
                _logger?.LogInformation("Retrieved {Size} bytes from commit {Commit}", bytes.Length, commitHash);
                return bytes;
            }
            catch (Exception gitEx)
            {
                _logger?.LogWarning("Git command failed. Exception type: {Type}, Message: {Message}", gitEx.GetType().Name, gitEx.Message);
                
                // Check if this is a path-related error
                if (gitEx is InvalidOperationException && 
                    (gitEx.Message.Contains("exists, but not") || 
                     gitEx.Message.Contains("exists on disk, but not") ||
                     gitEx.Message.Contains("fatal: path")))
                {
                    // Git error suggests the path format is wrong OR file didn't exist in that commit
                    _logger?.LogWarning("Path '{RelPath}' not found in commit, attempting to auto-detect correct path...", relPath);
                    
                    var correctPath = FindFileInCommit(commitHash, Path.GetFileName(filePath));
                    if (!string.IsNullOrEmpty(correctPath))
                    {
                        _logger?.LogInformation("Found file at: {CorrectPath}, retrying with correct path", correctPath);
                        using var stream = RunGitStream(_repoRoot, "show", $"{commitHash}:{correctPath}");
                        using var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        var bytes = ms.ToArray();
                        
                        _logger?.LogInformation("Retrieved {Size} bytes from commit {Commit} using corrected path", bytes.Length, commitHash);
                        return bytes;
                    }
                    
                    // File truly doesn't exist in that commit
                    _logger?.LogError("File '{FileName}' does not exist in commit {Commit}. It may have been added after this commit.", Path.GetFileName(filePath), commitHash);
                    throw new InvalidOperationException($"File '{Path.GetFileName(filePath)}' does not exist in commit {commitHash}. This file was likely added after this commit. Please select a more recent commit.", gitEx);
                }
                
                // Re-throw if not a path error
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to get file content at commit {Commit} for {FilePath}", commitHash, filePath);
            throw new InvalidOperationException($"Failed to get file content at commit {commitHash}: {ex.Message}", ex);
        }
    }
    
    /// <summary>
    /// Searches for a file in a specific commit by filename.
    /// </summary>
    private string? FindFileInCommit(string commitHash, string filename)
    {
        try
        {
            // List all files in the commit
            var output = RunGitAt(_repoRoot, "ls-tree", "-r", "--name-only", commitHash);
            var files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            _logger?.LogDebug("Searching for '{Filename}' in {Count} files from commit {Commit}", filename, files.Length, commitHash);
            
            // Find files matching the filename (case-insensitive, handle spaces)
            var matches = files.Where(f => 
            {
                var parts = f.Split('/');
                var lastPart = parts[parts.Length - 1];
                return string.Equals(lastPart, filename, StringComparison.OrdinalIgnoreCase);
            }).ToList();
            
            if (matches.Count == 1)
            {
                _logger?.LogInformation("Found exact match: {Path}", matches[0]);
                return matches[0];
            }
            else if (matches.Count > 1)
            {
                _logger?.LogWarning("Multiple files found with name '{Filename}': {Matches}", filename, string.Join(", ", matches));
                // Return the first match as a best guess
                return matches[0];
            }
            
            _logger?.LogWarning("No files found matching '{Filename}'", filename);
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Could not search for file in commit");
            return null;
        }
    }

    private string RunGitAt(string workingDir, params string[] args)
    {
        using var stream = RunGitStream(workingDir, args);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    /// <summary>
    /// Resolve a repository-relative path that Git understands, with fallbacks for nested files and casing.
    /// </summary>
    private string ResolveTrackedPath(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var relPath = NormalizePath(Path.GetRelativePath(_repoRoot, fullPath));

        // Quick exit if the straightforward relative path matches a tracked file
        var tracked = GetTrackedPaths();

        // Absolute compare to avoid losing nested folders when relative resolution goes wrong
        var absoluteMatch = tracked.FirstOrDefault(p =>
            PathsEqual(Path.Combine(_repoRoot, p), fullPath));
        if (absoluteMatch != null)
        {
            return absoluteMatch;
        }

        var exactMatch = tracked.FirstOrDefault(p => string.Equals(p, relPath, StringComparison.OrdinalIgnoreCase));
        if (exactMatch != null)
        {
            return exactMatch;
        }

        // Otherwise, try to find the best match based on filename and deepest matching suffix
        var fileName = Path.GetFileName(filePath);
        var bestMatch = tracked
            .Where(p => string.Equals(Path.GetFileName(p), fileName, StringComparison.OrdinalIgnoreCase))
            .Select(p => new { Path = p, Score = CalculateSuffixScore(relPath, p) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Path.Length)
            .FirstOrDefault(x => x.Score > 0);

        if (bestMatch != null)
        {
            _logger?.LogInformation("Resolved tracked path fallback: requested {Requested} -> using {Resolved}", relPath, bestMatch.Path);
            return bestMatch.Path;
        }

        // Fall back to the naive relative path
        _logger?.LogWarning("Could not find tracked path for {Requested}; falling back to relative path", relPath);
        return relPath;
    }

    private List<string> GetTrackedPaths()
    {
        try
        {
            var output = RunGitAt(_repoRoot, "ls-files", "--full-name");
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to list tracked files for repo {Repo}", _repoRoot);
            return new List<string>();
        }
    }

    /// <summary>
    /// Returns how many path segments match from the end of both paths.
    /// Higher score means deeper matching subpath.
    /// </summary>
    private int CalculateSuffixScore(string requested, string candidate)
    {
        var reqSegments = requested.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var candSegments = candidate.Split('/', StringSplitOptions.RemoveEmptyEntries);

        var max = Math.Min(reqSegments.Length, candSegments.Length);
        var score = 0;
        for (int i = 1; i <= max; i++)
        {
            if (string.Equals(reqSegments[^i], candSegments[^i], StringComparison.OrdinalIgnoreCase))
            {
                score++;
            }
            else
            {
                break;
            }
        }
        return score;
    }

    private static string NormalizePath(string path) =>
        path.Replace("\\", "/");

    private static bool PathsEqual(string pathA, string pathB) =>
        string.Equals(Path.GetFullPath(pathA).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                      Path.GetFullPath(pathB).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                      StringComparison.OrdinalIgnoreCase);

    private void WarnIfOutsideRepo(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        var normalizedRepo = _repoRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!fullPath.StartsWith(normalizedRepo, StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("File path {File} is outside resolved repo root {Repo}. Path resolution may fail.", fullPath, _repoRoot);
        }
    }

    /// <summary>
    /// Resolve the repository root from any path within the worktree.
    /// </summary>
    private string ResolveRepoRoot(string path)
    {
        string? topLevel = TryGitTopLevel(path);
        if (!string.IsNullOrEmpty(topLevel))
        {
            return topLevel;
        }

        var walked = WalkUpForGit(path);
        if (!string.IsNullOrEmpty(walked))
        {
            return walked;
        }

        throw new InvalidOperationException($"Path '{path}' is not inside a Git repository.");
    }

    private string? TryGitTopLevel(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = string.IsNullOrWhiteSpace(path) ? Environment.CurrentDirectory : path,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("rev-parse");
            psi.ArgumentList.Add("--show-toplevel");

            var proc = Process.Start(psi);
            if (proc == null) return null;

            var output = proc.StandardOutput.ReadToEnd().Trim();
            var error = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                return output;
            }

            _logger?.LogWarning("Could not resolve repo root from {Path}. Git error: {Error}", path, error);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to resolve git repo root from {Path}", path);
        }

        return null;
    }

    private string? WalkUpForGit(string path)
    {
        try
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists && dir.Parent != null)
            {
                dir = dir.Parent; // if a file path was passed, move to its directory
            }

            while (dir != null)
            {
                var gitDir = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(gitDir) || File.Exists(gitDir))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed walking up directories to find .git from {Path}", path);
        }

        return null;
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
    public string FilePath { get; set; } = string.Empty; // The relative path used for this commit
    public long FileSizeBytes { get; set; } = 0;
}
