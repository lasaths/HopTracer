using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Models;
using HopTracer.Web.Serialization;
using HopTracer.Web.Services;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("[controller]")]
public partial class GitController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<GitController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IGhxParser _parser;
    private readonly IDiffer _differ;
    private readonly IConverterService _converter;
    private readonly IFileValidationService _fileValidator;
    private readonly IAppDataStorageService _storage;

    // Regex to validate git commit hashes (7-40 hex characters)
    [GeneratedRegex(@"^[a-fA-F0-9]{7,40}$")]
    private static partial Regex GitHashRegex();

    public GitController(
        IWebHostEnvironment env, 
        ILogger<GitController> logger,
        ILoggerFactory loggerFactory,
        IGhxParser parser,
        IDiffer differ,
        IConverterService converter,
        IFileValidationService fileValidator,
        IAppDataStorageService storage)
    {
        _env = env;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _parser = parser;
        _differ = differ;
        _converter = converter;
        _fileValidator = fileValidator;
        _storage = storage;
    }

    [HttpPost("check")]
    public IActionResult Check([FromBody] PathRequest request)
    {
        if (string.IsNullOrEmpty(request.Path))
        {
            return Ok(new { is_repo = false });
        }

        if (!_fileValidator.TryValidateExistingPath(request.Path, out _, out var normalizedPath))
        {
            return Ok(new { is_repo = false });
        }

        try
        {
            var dirPath = Path.GetDirectoryName(normalizedPath) ?? normalizedPath;
            var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());
            return Ok(new { is_repo = wrapper.IsGitRepo() });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Git repo check failed for non-repo path: {Path}", normalizedPath);
            return Ok(new { is_repo = false });
        }
    }

    [HttpPost("commits")]
    public IActionResult GetCommits([FromBody] PathRequest request)
    {
        if (string.IsNullOrEmpty(request.Path))
        {
            return NotFound(new { error = "File path is required" });
        }

        if (!_fileValidator.TryValidateExistingPath(request.Path, out var validationError, out var normalizedPath))
        {
            return NotFound(new { error = validationError });
        }

        var dirPath = Path.GetDirectoryName(normalizedPath) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());
        
        try
        {
            // Load full history for the selected file in the source picker.
            var commits = wrapper.GetCommits(normalizedPath, 0);
            return Ok(new { commits = commits });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get commits for {Path}", normalizedPath);
            return Ok(new { commits = new List<CommitInfo>() });
        }
    }

    [HttpPost("compare")]
    public async Task<IActionResult> Compare([FromBody] GitCompareRequest request)
    {
        if (string.IsNullOrEmpty(request.Path) || string.IsNullOrEmpty(request.HashOld))
        {
            return BadRequest(new { error = "Missing parameters" });
        }

        if (!_fileValidator.TryValidateExistingPath(request.Path, out var validationError, out var normalizedPath))
        {
            return BadRequest(new { error = validationError });
        }

        if (!ValidateGitHash(request.HashOld))
        {
            return BadRequest(new { error = "Invalid commit hash format" });
        }

        if (!string.IsNullOrEmpty(request.HashNew) && !ValidateGitHash(request.HashNew))
        {
            return BadRequest(new { error = "Invalid commit hash format" });
        }

        var tempFiles = new List<string>();
        try
        {
            var stageTimings = new List<DiffStageTiming>();
            var oldSource = await PrepareDiffSourceAsync(normalizedPath, request.HashOld, "old", tempFiles);
            var newSource = await PrepareDiffSourceAsync(normalizedPath, request.HashNew, "new", tempFiles);

            // Parse and diff
            var sw = Stopwatch.StartNew();
            var graphOld = _parser.Parse(oldSource.PreparedPath);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "parse_old", DurationMs = sw.ElapsedMilliseconds });

            sw.Restart();
            var graphNew = _parser.Parse(newSource.PreparedPath);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "parse_new", DurationMs = sw.ElapsedMilliseconds });

            sw.Restart();
            var diff = _differ.DiffDetailed(graphOld, graphNew);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "diff_compute", DurationMs = sw.ElapsedMilliseconds });

            var diffData = new DiffResponse
            {
                Nodes = diff.Nodes,
                Edges = diff.Edges,
                Meta = new DiffMeta
                {
                    GeneratedAt = DateTime.Now.ToString("o"),
                    NodeCount = diff.Nodes.Count,
                    EdgeCount = diff.Edges.Count,
                    SourcePathOld = normalizedPath,
                    SourcePathNew = normalizedPath,
                    SourceTypeOld = "commit",
                    SourceTypeNew = string.IsNullOrEmpty(request.HashNew) ? "file" : "commit",
                    SourceHashOld = request.HashOld,
                    SourceHashNew = string.IsNullOrEmpty(request.HashNew) ? null : request.HashNew,
                    Diagnostics = diff.Diagnostics,
                    TopRisks = diff.TopRisks,
                    RiskSummary = diff.RiskSummary,
                    StageTimings = stageTimings
                },
                OldMeta = graphOld.Metadata,
                NewMeta = graphNew.Metadata
            };

            return Ok(new { diff_data = diffData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Git comparison");
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            CleanupTempFiles(tempFiles);
        }
    }

    [HttpPost("file_info")]
    public IActionResult GetFileInfo([FromBody] PathRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return BadRequest(new { error = "File path is required" });
        }

        if (!_fileValidator.TryValidateExistingPath(request.Path, out var validationError, out var normalizedPath))
        {
            return BadRequest(new { error = validationError });
        }

        try
        {
            var info = new FileInfo(normalizedPath);
            return Ok(new
            {
                path = normalizedPath,
                size_bytes = info.Exists ? info.Length : 0,
                modified_utc = info.Exists ? info.LastWriteTimeUtc.ToString("o") : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get file info for {Path}", normalizedPath);
            return StatusCode(500, new { error = "Failed to read file info" });
        }
    }

    [HttpPost("view_diff")]
    public async Task<IActionResult> ViewDiff(
        [FromForm] string? path,
        [FromForm] string? hash_old,
        [FromForm] string? path_old,
        [FromForm] string? path_new,
        [FromForm] string? hash_new)
    {
        var requestedPathOld = string.IsNullOrWhiteSpace(path_old) ? path : path_old;
        var requestedPathNew = string.IsNullOrWhiteSpace(path_new) ? path : path_new;

        if (string.IsNullOrWhiteSpace(requestedPathOld) || string.IsNullOrWhiteSpace(requestedPathNew))
        {
            return BadRequest("Both old and new paths are required.");
        }

        if (!_fileValidator.TryValidateExistingPath(requestedPathOld, out var oldValidationError, out var normalizedPathOld))
        {
            _logger.LogWarning("Invalid old path for view_diff: {Path}", requestedPathOld);
            return BadRequest(oldValidationError);
        }

        if (!_fileValidator.TryValidateExistingPath(requestedPathNew, out var newValidationError, out var normalizedPathNew))
        {
            _logger.LogWarning("Invalid new path for view_diff: {Path}", requestedPathNew);
            return BadRequest(newValidationError);
        }

        if (!string.IsNullOrWhiteSpace(hash_old) && !ValidateGitHash(hash_old))
        {
            _logger.LogWarning("Invalid old git hash for view_diff: {Hash}", hash_old);
            return BadRequest("Invalid old commit hash format");
        }

        if (!string.IsNullOrWhiteSpace(hash_new) && !ValidateGitHash(hash_new))
        {
            _logger.LogWarning("Invalid new git hash for view_diff: {Hash}", hash_new);
            return BadRequest("Invalid new commit hash format");
        }

        var tempFiles = new List<string>();
        try
        {
            var oldSource = await PrepareDiffSourceAsync(normalizedPathOld, hash_old, "old", tempFiles);
            var newSource = await PrepareDiffSourceAsync(normalizedPathNew, hash_new, "new", tempFiles);

            var stageTimings = new List<DiffStageTiming>();
            var sw = Stopwatch.StartNew();
            var graphOld = _parser.Parse(oldSource.PreparedPath);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "parse_old", DurationMs = sw.ElapsedMilliseconds });

            sw.Restart();
            var graphNew = _parser.Parse(newSource.PreparedPath);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "parse_new", DurationMs = sw.ElapsedMilliseconds });

            sw.Restart();
            var diff = _differ.DiffDetailed(graphOld, graphNew);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "diff_compute", DurationMs = sw.ElapsedMilliseconds });

            var primaryCommitHash = !string.IsNullOrWhiteSpace(hash_old) ? hash_old : hash_new;
            var primaryCommitInfo = !string.IsNullOrWhiteSpace(hash_old) ? oldSource.CommitInfo : newSource.CommitInfo;

            var diffData = new DiffResponse
            {
                Nodes = diff.Nodes,
                Edges = diff.Edges,
                Meta = new DiffMeta
                {
                    GeneratedAt = DateTime.Now.ToString("o"),
                    NodeCount = diff.Nodes.Count,
                    EdgeCount = diff.Edges.Count,
                    FileOld = BuildSourceLabel(normalizedPathOld, hash_old),
                    FileNew = BuildSourceLabel(normalizedPathNew, hash_new),
                    CommitHash = primaryCommitHash,
                    CommitAuthor = primaryCommitInfo?.Author,
                    CommitDate = primaryCommitInfo?.Date,
                    CommitMessage = primaryCommitInfo?.Message,
                    SourcePathOld = normalizedPathOld,
                    SourcePathNew = normalizedPathNew,
                    SourceTypeOld = string.IsNullOrWhiteSpace(hash_old) ? "file" : "commit",
                    SourceTypeNew = string.IsNullOrWhiteSpace(hash_new) ? "file" : "commit",
                    SourceHashOld = string.IsNullOrWhiteSpace(hash_old) ? null : hash_old,
                    SourceHashNew = string.IsNullOrWhiteSpace(hash_new) ? null : hash_new,
                    Diagnostics = diff.Diagnostics,
                    TopRisks = diff.TopRisks,
                    RiskSummary = diff.RiskSummary,
                    StageTimings = stageTimings
                },
                OldMeta = graphOld.Metadata,
                NewMeta = graphNew.Metadata
            };

            var fileInfo = _env.WebRootFileProvider.GetFileInfo("diff_viewer.html");
            if (!fileInfo.Exists)
            {
                _logger.LogError("diff_viewer.html template not found");
                throw new FileNotFoundException("Template not found");
            }

            string html;
            using (var stream = fileInfo.CreateReadStream())
            using (var reader = new StreamReader(stream))
            {
                html = await reader.ReadToEndAsync();
            }

            var jsonText = JsonSerializer.Serialize(diffData, AppJsonContext.Default.DiffResponse);
            html = html.Replace("{json_data}", jsonText);

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating diff from Git: {Message}", ex.Message);
            return StatusCode(500, $"Error generating diff: {ex.Message}");
        }
        finally
        {
            CleanupTempFiles(tempFiles);
        }
    }

    private async Task<PreparedDiffSource> PrepareDiffSourceAsync(
        string normalizedPath,
        string? commitHash,
        string sourceTag,
        List<string> tempFiles)
    {
        var extension = Path.GetExtension(normalizedPath).ToLowerInvariant();
        var preparedPath = normalizedPath;
        CommitInfo? commitInfo = null;

        if (!string.IsNullOrWhiteSpace(commitHash))
        {
            var dirPath = Path.GetDirectoryName(normalizedPath) ?? normalizedPath;
            var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());
            if (!wrapper.IsGitRepo())
            {
                throw new InvalidOperationException($"Path is not in a Git repository: {normalizedPath}");
            }

            var commits = wrapper.GetCommits(normalizedPath, 200);
            commitInfo = commits.FirstOrDefault(c => c.Hash == commitHash || c.Hash.StartsWith(commitHash, StringComparison.OrdinalIgnoreCase));

            var content = wrapper.GetFileContentAtCommit(commitHash, normalizedPath);
            var tempPath = Path.Combine(_storage.UploadsPath, $"git_{sourceTag}_{Guid.NewGuid():N}{extension}");
            await System.IO.File.WriteAllBytesAsync(tempPath, content);
            tempFiles.Add(tempPath);
            preparedPath = tempPath;
        }
        else
        {
            // Copy current file to temp before parsing/conversion to avoid touching repo files.
            var tempPath = Path.Combine(_storage.UploadsPath, $"git_{sourceTag}_current_{Guid.NewGuid():N}{extension}");
            System.IO.File.Copy(normalizedPath, tempPath, overwrite: true);
            tempFiles.Add(tempPath);
            preparedPath = tempPath;
        }

        if (preparedPath.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
        {
            if (!_converter.TryCheckDependencies(out var dependencyMessage))
            {
                throw new InvalidOperationException(dependencyMessage);
            }

            var convertedPath = _converter.ConvertGhToGhx(preparedPath);
            if (!string.Equals(convertedPath, preparedPath, StringComparison.OrdinalIgnoreCase))
            {
                tempFiles.Add(convertedPath);
            }
            preparedPath = convertedPath;
        }

        return new PreparedDiffSource(preparedPath, commitInfo);
    }

    private void CleanupTempFiles(List<string> tempFiles)
    {
        foreach (var tempPath in tempFiles.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete temporary file: {TempPath}", tempPath);
            }
        }
    }

    private static string BuildSourceLabel(string normalizedPath, string? commitHash)
    {
        var fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(commitHash))
        {
            return $"{fileName} (current)";
        }

        var shortHash = commitHash.Length > 7 ? commitHash[..7] : commitHash;
        return $"{fileName} @ {shortHash}";
    }

    private sealed record PreparedDiffSource(string PreparedPath, CommitInfo? CommitInfo);

    private bool ValidateGitHash(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return false;

        return GitHashRegex().IsMatch(hash);
    }
}

public class PathRequest
{
    public string Path { get; set; } = string.Empty;
}

public class GitCompareRequest
{
    public string Path { get; set; } = string.Empty;
    public string HashOld { get; set; } = string.Empty;
    public string HashNew { get; set; } = string.Empty;
}
