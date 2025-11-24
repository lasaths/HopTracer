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

    public GitController(
        IWebHostEnvironment env, 
        ILogger<GitController> logger,
        ILoggerFactory loggerFactory,
        IGhxParser parser,
        IDiffer differ)
    {
        _env = env;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _parser = parser;
        _differ = differ;
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

        var dirPath = Path.GetDirectoryName(path) ?? "";
        var wrapper = new GitWrapper(dirPath, _loggerFactory.CreateLogger<GitWrapper>());

        try
        {
            // Get old content
            var contentOld = wrapper.GetFileContentAtCommit(hash_old, path);
            var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);
            
            var pathOld = Path.Combine(uploadsPath, $"git_{hash_old}.ghx");
            await System.IO.File.WriteAllBytesAsync(pathOld, contentOld);

            // Use current file for new content
            var pathNew = path;

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

            // Load the diff viewer template
            var fileInfo = _env.WebRootFileProvider.GetFileInfo("diff_viewer.html");
            if (!fileInfo.Exists) throw new FileNotFoundException("Template not found");
            
            string html;
            using (var stream = fileInfo.CreateReadStream())
            using (var reader = new StreamReader(stream))
            {
                html = await reader.ReadToEndAsync();
            }

            // Inject JSON data
            var jsonText = JsonSerializer.Serialize(diffData, AppJsonContext.Default.DiffResponse);
            html = html.Replace("{json_data}", jsonText);

            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating diff");
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
