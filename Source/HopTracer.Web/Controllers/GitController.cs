using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Models;
using HopTracer.Web.Serialization;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class GitController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<GitController> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IGhxParser _parser;
    private readonly IDiffer _differ;
    private readonly IConverterService _converter;

    public GitController(
        IWebHostEnvironment env, 
        ILogger<GitController> logger,
        ILoggerFactory loggerFactory,
        IGhxParser parser,
        IDiffer differ,
        IConverterService converter)
    {
        _env = env;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _parser = parser;
        _differ = differ;
        _converter = converter;
    }

    [HttpPost("check")]
    public IActionResult Check([FromBody] PathRequest request)
    {
        if (string.IsNullOrEmpty(request.Path) || !System.IO.File.Exists(request.Path))
        {
            return Ok(new { is_repo = false });
        }

        var dirPath = Path.GetDirectoryName(request.Path) ?? request.Path;
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());
        return Ok(new { is_repo = wrapper.IsGitRepo() });
    }

    [HttpPost("commits")]
    public IActionResult GetCommits([FromBody] PathRequest request)
    {
        if (string.IsNullOrEmpty(request.Path) || !System.IO.File.Exists(request.Path))
        {
            return NotFound(new { error = "File not found" });
        }

        var dirPath = Path.GetDirectoryName(request.Path) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());
        
        try
        {
            var commits = wrapper.GetCommits(request.Path);
            return Ok(new { commits = commits });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get commits");
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

        var dirPath = Path.GetDirectoryName(request.Path) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());

        try
        {
            // Get old content
            var contentOld = wrapper.GetFileContentAtCommit(request.HashOld, request.Path);
            var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            
            var pathOld = Path.Combine(uploadsPath, $"git_{request.HashOld}.ghx");
            await System.IO.File.WriteAllBytesAsync(pathOld, contentOld);

            // Get new content
            string pathNew;
            if (!string.IsNullOrEmpty(request.HashNew))
            {
                var contentNew = wrapper.GetFileContentAtCommit(request.HashNew, request.Path);
                pathNew = Path.Combine(uploadsPath, $"git_{request.HashNew}.ghx");
                await System.IO.File.WriteAllBytesAsync(pathNew, contentNew);
            }
            else
            {
                pathNew = request.Path;
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

        // Validate that the file exists
        if (!System.IO.File.Exists(path))
        {
            _logger.LogError("File does not exist: {Path}", path);
            return BadRequest($"File not found: {path}");
        }

        var dirPath = Path.GetDirectoryName(path) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());

        // Check if it's a git repo
        if (!wrapper.IsGitRepo())
        {
            _logger.LogError("Path is not in a Git repository: {Path}", path);
            return BadRequest($"Path is not in a Git repository: {path}");
        }

        try
        {
            _logger.LogInformation("Fetching file content at commit {Commit} for {Path}", hash_old, path);
            
            // Get commit details
            var commits = wrapper.GetCommits(path, 100); // Get enough to find our commit
            var commitInfo = commits.FirstOrDefault(c => c.Hash == hash_old || c.Hash.StartsWith(hash_old));
            
            // Get old content from git
            var contentOld = wrapper.GetFileContentAtCommit(hash_old, path);
            
            var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            
            // Determine file extension from original path
            var originalExtension = Path.GetExtension(path).ToLower();
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
            var pathNew = path;
            
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
