using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Models;
using HopTracer.Web.Serialization;
using System.Diagnostics;
using System.Text.Json;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompareController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CompareController> _logger;
    private readonly IGhxParser _parser;
    private readonly IDiffer _differ;
    private readonly IConverterService _converter;
    private const long MaxFileSize = 500 * 1024 * 1024; // 500MB
    
    // Store the last uploaded file path for Git integration
    private static string? _lastUploadedOldFilePath;

    public CompareController(
        IWebHostEnvironment env, 
        ILogger<CompareController> logger,
        IGhxParser parser,
        IDiffer differ,
        IConverterService converter)
    {
        _env = env;
        _logger = logger;
        _parser = parser;
        _differ = differ;
        _converter = converter;
    }

    [HttpPost]
    [Route("/compare")]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<IActionResult> Compare(IFormFile? file_old, IFormFile? file_new, [FromForm] string? path_old, [FromForm] string? path_new)
    {
        try
        {
            string finalPathOld;
            string finalPathNew;
            string originalFileNameOld;
            string originalFileNameNew;

            // Handle Old File
            if (!string.IsNullOrEmpty(path_old) && System.IO.File.Exists(path_old))
            {
                finalPathOld = path_old;
                originalFileNameOld = Path.GetFileName(path_old);
                _lastUploadedOldFilePath = path_old; // Store for Git
                _logger.LogInformation("Using local old file: {Path}", path_old);
            }
            else if (file_old != null)
            {
                var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);
                finalPathOld = Path.Combine(uploadsPath, file_old.FileName);
                
                using (var stream = new FileStream(finalPathOld, FileMode.Create))
                {
                    await file_old.CopyToAsync(stream);
                }
                originalFileNameOld = file_old.FileName;
                _lastUploadedOldFilePath = file_old.FileName; // Store for Git (though less useful without full path)
                _logger.LogInformation("Uploaded old file: {FileName}", file_old.FileName);
            }
            else
            {
                return await ReturnErrorPage("Old file is required (either upload or path)");
            }

            // Handle New File
            if (!string.IsNullOrEmpty(path_new) && System.IO.File.Exists(path_new))
            {
                finalPathNew = path_new;
                originalFileNameNew = Path.GetFileName(path_new);
                _logger.LogInformation("Using local new file: {Path}", path_new);
            }
            else if (file_new != null)
            {
                var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);
                finalPathNew = Path.Combine(uploadsPath, file_new.FileName);

                using (var stream = new FileStream(finalPathNew, FileMode.Create))
                {
                    await file_new.CopyToAsync(stream);
                }
                originalFileNameNew = file_new.FileName;
                _logger.LogInformation("Uploaded new file: {FileName}", file_new.FileName);
            }
            else
            {
                return await ReturnErrorPage("New file is required (either upload or path)");
            }

            // Convert .gh to .ghx if necessary
            if (finalPathOld.ToLower().EndsWith(".gh"))
            {
                finalPathOld = _converter.ConvertGhToGhx(finalPathOld);
            }

            if (finalPathNew.ToLower().EndsWith(".gh"))
            {
                finalPathNew = _converter.ConvertGhToGhx(finalPathNew);
            }

            // Parse and diff
            var graphOld = _parser.Parse(finalPathOld);
            var graphNew = _parser.Parse(finalPathNew);

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
                    FileOld = originalFileNameOld,
                    FileNew = originalFileNameNew,
                    CommitHash = null // Not a Git comparison
                },
                OldMeta = graphOld.Metadata,
                NewMeta = graphNew.Metadata
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
            _logger.LogError(ex, "Error during comparison");
            return await ReturnErrorPage($"Error: {ex.Message}");
        }
    }

    private async Task<IActionResult> ReturnErrorPage(string message)
    {
        var fileInfo = _env.WebRootFileProvider.GetFileInfo("error.html");
        if (!fileInfo.Exists)
        {
            // Fallback if error.html is missing
            return StatusCode(500, $"Error: {message}");
        }

        string html;
        using (var stream = fileInfo.CreateReadStream())
        using (var reader = new StreamReader(stream))
        {
            html = await reader.ReadToEndAsync();
        }

        html = html.Replace("{error_message}", System.Net.WebUtility.HtmlEncode(message));
        return Content(html, "text/html");
    }
}
