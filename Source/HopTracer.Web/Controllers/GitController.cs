using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Models;
using HopTracer.Web.Serialization;
using HopTracer.Web.Services;
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
        IFileValidationService fileValidator)
    {
        _env = env;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _parser = parser;
        _differ = differ;
        _converter = converter;
        _fileValidator = fileValidator;
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

        var dirPath = Path.GetDirectoryName(normalizedPath) ?? normalizedPath;
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());
        return Ok(new { is_repo = wrapper.IsGitRepo() });
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
            var commits = wrapper.GetCommits(normalizedPath);
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

        var dirPath = Path.GetDirectoryName(normalizedPath) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());

        try
        {
            // Get old content
            var contentOld = wrapper.GetFileContentAtCommit(request.HashOld, normalizedPath);
            var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            
            var pathOld = Path.Combine(uploadsPath, $"git_{request.HashOld}.ghx");
            await System.IO.File.WriteAllBytesAsync(pathOld, contentOld);

            // Get new content
            string pathNew;
            if (!string.IsNullOrEmpty(request.HashNew))
            {
                var contentNew = wrapper.GetFileContentAtCommit(request.HashNew, normalizedPath);
                pathNew = Path.Combine(uploadsPath, $"git_{request.HashNew}.ghx");
                await System.IO.File.WriteAllBytesAsync(pathNew, contentNew);
            }
            else
            {
                pathNew = normalizedPath;
            }

            // Parse and diff
            var graphOld = _parser.Parse(pathOld);
            var graphNew = _parser.Parse(pathNew);

            var (nodes, edges) = _differ.Diff(graphOld, graphNew);

            var diffData = new DiffResponse
            {
                Nodes = nodes,
                Edges = edges,
                Meta = new DiffMeta
                {
                    GeneratedAt = DateTime.Now.ToString("o"),
                    NodeCount = nodes.Count,
                    EdgeCount = edges.Count
                }
            };

            return Ok(new { diff_data = diffData });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Git comparison");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("view_diff")]
    public async Task<IActionResult> ViewDiff([FromForm] string path, [FromForm] string hash_old)
    {
        if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(hash_old))
        {
            return BadRequest("Missing parameters for Git diff");
        }

        if (!_fileValidator.TryValidateExistingPath(path, out var validationError, out var normalizedPath))
        {
            _logger.LogWarning("Invalid file path for view_diff: {Path}", path);
            return BadRequest(validationError);
        }

        if (!ValidateGitHash(hash_old))
        {
            _logger.LogWarning("Invalid git hash for view_diff: {Hash}", hash_old);
            return BadRequest("Invalid commit hash format");
        }

        var dirPath = Path.GetDirectoryName(normalizedPath) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());

        // Check if it's a git repo
        if (!wrapper.IsGitRepo())
        {
            _logger.LogError("Path is not in a Git repository: {Path}", normalizedPath);
            return BadRequest($"Path is not in a Git repository: {normalizedPath}");
        }

        try
        {
            _logger.LogInformation("Fetching file content at commit {Commit} for {Path}", hash_old, normalizedPath);
            
            // Get commit details
            var commits = wrapper.GetCommits(normalizedPath, 100); // Get enough to find our commit
            var commitInfo = commits.FirstOrDefault(c => c.Hash == hash_old || c.Hash.StartsWith(hash_old));
            
            // Get old content from git
            var contentOld = wrapper.GetFileContentAtCommit(hash_old, normalizedPath);
            
            var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            
            // Determine file extension from original path
            var originalExtension = Path.GetExtension(normalizedPath).ToLower();
            var isBinaryGh = originalExtension == ".gh";
            
            // Save the file with appropriate extension
            var tempFileName = $"git_{hash_old}{originalExtension}";
            var pathOld = Path.Combine(uploadsPath, tempFileName);
            await System.IO.File.WriteAllBytesAsync(pathOld, contentOld);
            
            _logger.LogInformation("Saved old version to: {PathOld} ({Size} bytes)", pathOld, contentOld.Length);
            
            // Convert .gh to .ghx if necessary
            if (isBinaryGh)
            {
                _logger.LogInformation("Converting binary .gh to .ghx...");
                try
                {
                    pathOld = _converter.ConvertGhToGhx(pathOld);
                    _logger.LogInformation("Converted to: {PathOld}", pathOld);
                }
                catch (Exception convEx)
                {
                    _logger.LogError(convEx, "Failed to convert .gh to .ghx");
                    return StatusCode(500, $"Failed to convert .gh file from Git history: {convEx.Message}");
                }
            }

            // Use current file for new content
            var pathNew = normalizedPath;
            
            // Convert current file if it's also .gh - copy to temp first!
            if (pathNew.ToLower().EndsWith(".gh"))
            {
                _logger.LogInformation("Converting current .gh file to .ghx...");
                try
                {
                    // Copy to temp directory to avoid modifying the repo
                    var tempCurrentFileName = $"git_current_{hash_old}.gh";
                    var tempCurrentPath = Path.Combine(uploadsPath, tempCurrentFileName);
                    System.IO.File.Copy(pathNew, tempCurrentPath, overwrite: true);
                    _logger.LogDebug("Copied current file to temp: {TempPath}", tempCurrentPath);
                    
                    pathNew = _converter.ConvertGhToGhx(tempCurrentPath);
                    _logger.LogInformation("Converted current file to: {PathNew}", pathNew);
                }
                catch (Exception convEx)
                {
                    _logger.LogError(convEx, "Failed to convert current .gh file");
                    return StatusCode(500, $"Failed to convert current .gh file: {convEx.Message}");
                }
            }
            
            _logger.LogInformation("Using current file as new version: {PathNew}", pathNew);

            // Parse and diff
            _logger.LogInformation("Parsing old file: {PathOld}", pathOld);
            var graphOld = _parser.Parse(pathOld);
            
            _logger.LogInformation("Parsing new file: {PathNew}", pathNew);
            var graphNew = _parser.Parse(pathNew);

            _logger.LogInformation("Computing diff...");
            var (nodes, edges) = _differ.Diff(graphOld, graphNew);

            var diffData = new DiffResponse
            {
                Nodes = nodes,
                Edges = edges,
                Meta = new DiffMeta
                {
                    GeneratedAt = DateTime.Now.ToString("o"),
                    NodeCount = nodes.Count,
                    EdgeCount = edges.Count,
                    FileOld = $"{Path.GetFileName(path)} @ {hash_old.Substring(0, 7)}",
                    FileNew = $"{Path.GetFileName(path)} (current)",
                    CommitHash = hash_old,
                    CommitAuthor = commitInfo?.Author,
                    CommitDate = commitInfo?.Date,
                    CommitMessage = commitInfo?.Message
                }
            };
            
            _logger.LogInformation("Diff completed: {NodeCount} nodes, {EdgeCount} edges", nodes.Count, edges.Count);

            // Load the diff viewer template
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

            // Inject JSON data
            var jsonText = JsonSerializer.Serialize(diffData, AppJsonContext.Default.DiffResponse);
            html = html.Replace("{json_data}", jsonText);

            // Cleanup temporary files
            try
            {
                // Clean up the git-retrieved file (both .gh and .ghx versions)
                var tempGhPath = Path.Combine(uploadsPath, $"git_{hash_old}.gh");
                var tempGhxPath = Path.Combine(uploadsPath, $"git_{hash_old}.ghx");
                var tempCurrentGhPath = Path.Combine(uploadsPath, $"git_current_{hash_old}.gh");
                var tempCurrentGhxPath = Path.Combine(uploadsPath, $"git_current_{hash_old}.ghx");
                
                foreach (var tempPath in new[] { tempGhPath, tempGhxPath, tempCurrentGhPath, tempCurrentGhxPath })
                {
                    if (System.IO.File.Exists(tempPath))
                    {
                        System.IO.File.Delete(tempPath);
                        _logger.LogDebug("Deleted temporary file: {TempPath}", tempPath);
                    }
                }
            }
            catch (Exception cleanupEx)
            {
                _logger.LogWarning(cleanupEx, "Could not cleanup temporary files");
                // Ignore cleanup errors
            }

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating diff from Git: {Message}", ex.Message);
            return StatusCode(500, $"Error generating diff: {ex.Message}");
        }
    }

    private bool ValidateFilePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Check for path traversal
        if (path.Contains(".."))
        {
            _logger.LogWarning("Path traversal attempt detected: {Path}", path);
            return false;
        }

        if (!System.IO.File.Exists(path))
            return false;

        var extension = Path.GetExtension(path).ToLowerInvariant();
        return AllowedExtensions.Contains(extension);
    }

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
