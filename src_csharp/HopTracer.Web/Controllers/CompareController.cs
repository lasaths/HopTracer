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
    public async Task<IActionResult> Compare(IFormFile file_old, IFormFile file_new)
    {
        if (file_old == null || file_new == null)
        {
            return BadRequest("Both files are required");
        }

        try
        {
            var uploadsPath = Path.Combine(_env.ContentRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            var pathOld = Path.Combine(uploadsPath, file_old.FileName);
            var pathNew = Path.Combine(uploadsPath, file_new.FileName);

            // Save uploaded files
            using (var stream = new FileStream(pathOld, FileMode.Create))
            {
                await file_old.CopyToAsync(stream);
            }

            using (var stream = new FileStream(pathNew, FileMode.Create))
            {
                await file_new.CopyToAsync(stream);
            }

            // Convert .gh to .ghx if necessary
            if (pathOld.ToLower().EndsWith(".gh"))
            {
                pathOld = _converter.ConvertGhToGhx(pathOld);
            }

            if (pathNew.ToLower().EndsWith(".gh"))
            {
                pathNew = _converter.ConvertGhToGhx(pathNew);
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
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }
}
