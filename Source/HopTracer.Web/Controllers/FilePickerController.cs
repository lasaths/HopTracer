using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Services;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilePickerController : ControllerBase
{
    private readonly ILogger<FilePickerController> _logger;
    private static string? _lastUploadedOldFilePath;

    public FilePickerController(ILogger<FilePickerController> logger)
    {
        _logger = logger;
    }

    [HttpPost("capture-file-path")]
    public IActionResult CaptureFilePath(IFormFile file)
    {
        if (file == null)
        {
            return BadRequest(new { error = "No file provided" });
        }
        
        _lastUploadedOldFilePath = file.FileName;
        _logger.LogInformation("Captured file path: {FileName}", file.FileName);
        
        return Ok(new { path = file.FileName });
    }
    
    [HttpGet("last-file-path")]
    public IActionResult GetLastFilePath()
    {
        if (string.IsNullOrEmpty(_lastUploadedOldFilePath))
        {
            return NotFound(new { error = "No file has been uploaded yet" });
        }
        
        return Ok(new { path = _lastUploadedOldFilePath });
    }
    
    [HttpGet("pick-native-file")]
    public async Task<IActionResult> PickNativeFile([FromServices] INativeIntegration nativeIntegration)
    {
        try
        {
            _logger.LogInformation("Native picker requested");
            var path = await nativeIntegration.PickFileAsync("Select Grasshopper file");
            _logger.LogInformation("Native picker returned: {Path}", path ?? "(null)");
            
            if (!string.IsNullOrEmpty(path))
            {
                _lastUploadedOldFilePath = path;
                _logger.LogInformation("Native picker selected: {Path}", path);
                return Ok(new { path = path });
            }
            
            _logger.LogWarning("Native picker returned null/empty path");
            var message = "No file selected or native picker unavailable in this build.";
            return NotFound(new { error = message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error picking file");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
