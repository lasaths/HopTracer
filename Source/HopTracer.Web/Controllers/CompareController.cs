using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Models;
using HopTracer.Web.Serialization;
using HopTracer.Web.Services;
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
    private readonly IFileValidationService _fileValidator;
    private readonly IFileSelectionCache _fileSelectionCache;
    private const long MaxFileSize = 100 * 1024 * 1024; // 100MB - reasonable limit for GH files

    public CompareController(
        IWebHostEnvironment env, 
        ILogger<CompareController> logger,
        IGhxParser parser,
        IDiffer differ,
        IConverterService converter,
        IFileValidationService fileValidator,
        IFileSelectionCache fileSelectionCache)
    {
        _env = env;
        _logger = logger;
        _parser = parser;
        _differ = differ;
        _converter = converter;
        _fileValidator = fileValidator;
        _fileSelectionCache = fileSelectionCache;
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
            var stageTimings = new List<DiffStageTiming>();

            // Handle Old File
            if (!string.IsNullOrEmpty(path_old))
            {
                if (!_fileValidator.TryValidateExistingPath(path_old, out var pathError, out var normalizedPath))
                {
                    return await ReturnErrorPage(pathError);
                }
                finalPathOld = normalizedPath;
                originalFileNameOld = Path.GetFileName(normalizedPath);
                _fileSelectionCache.Record(finalPathOld);
                _logger.LogInformation("Using local old file: {Path}", normalizedPath);
            }
            else if (file_old != null)
            {
                if (!_fileValidator.TryValidateUpload(file_old, MaxFileSize, out var fileError))
                {
                    return await ReturnErrorPage(fileError);
                }
                
                var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);
                var safeFileName = _fileValidator.SanitizeFileName(file_old.FileName);
                finalPathOld = Path.Combine(uploadsPath, safeFileName);
                
                await using (var stream = new FileStream(finalPathOld, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await file_old.CopyToAsync(stream);
                }
                originalFileNameOld = file_old.FileName;
                _fileSelectionCache.Record(finalPathOld);
                _logger.LogInformation("Uploaded old file: {FileName}", file_old.FileName);
            }
            else
            {
                return await ReturnErrorPage("Old file is required (either upload or path)");
            }

            // Handle New File
            if (!string.IsNullOrEmpty(path_new))
            {
                if (!_fileValidator.TryValidateExistingPath(path_new, out var pathError, out var normalizedPath))
                {
                    return await ReturnErrorPage(pathError);
                }
                finalPathNew = normalizedPath;
                originalFileNameNew = Path.GetFileName(normalizedPath);
                _fileSelectionCache.Record(finalPathNew);
                _logger.LogInformation("Using local new file: {Path}", normalizedPath);
            }
            else if (file_new != null)
            {
                if (!_fileValidator.TryValidateUpload(file_new, MaxFileSize, out var fileError))
                {
                    return await ReturnErrorPage(fileError);
                }
                
                var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
                Directory.CreateDirectory(uploadsPath);
                var safeFileName = _fileValidator.SanitizeFileName(file_new.FileName);
                finalPathNew = Path.Combine(uploadsPath, safeFileName);

                await using (var stream = new FileStream(finalPathNew, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
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
                if (!_converter.TryCheckDependencies(out var dependencyMessage))
                {
                    return await ReturnErrorPage(dependencyMessage);
                }
                finalPathOld = _converter.ConvertGhToGhx(finalPathOld);
                _fileSelectionCache.Record(finalPathOld);
            }

            if (finalPathNew.ToLower().EndsWith(".gh"))
            {
                if (!_converter.TryCheckDependencies(out var dependencyMessage))
                {
                    return await ReturnErrorPage(dependencyMessage);
                }
                finalPathNew = _converter.ConvertGhToGhx(finalPathNew);
                _fileSelectionCache.Record(finalPathNew);
            }

            // Parse and diff
            var sw = Stopwatch.StartNew();
            var graphOld = _parser.Parse(finalPathOld);
            sw.Stop();
            stageTimings.Add(new DiffStageTiming { Name = "parse_old", DurationMs = sw.ElapsedMilliseconds });

            sw.Restart();
            var graphNew = _parser.Parse(finalPathNew);
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
                    FileOld = originalFileNameOld,
                    FileNew = originalFileNameNew,
                    CommitHash = null, // Not a Git comparison
                    SourcePathOld = finalPathOld,
                    SourcePathNew = finalPathNew,
                    SourceTypeOld = "file",
                    SourceTypeNew = "file",
                    SourceHashOld = null,
                    SourceHashNew = null,
                    Diagnostics = diff.Diagnostics,
                    TopRisks = diff.TopRisks,
                    RiskSummary = diff.RiskSummary,
                    StageTimings = stageTimings
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
