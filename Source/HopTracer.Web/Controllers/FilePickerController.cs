using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Services;
using HopTracer.Web.Services;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FilePickerController : ControllerBase
{
    private const long MaxFileSize = 100 * 1024 * 1024;
    private readonly ILogger<FilePickerController> _logger;
    private readonly IFileValidationService _fileValidator;
    private readonly IFileSelectionCache _fileSelectionCache;

    public FilePickerController(
        ILogger<FilePickerController> logger,
        IFileValidationService fileValidator,
        IFileSelectionCache fileSelectionCache)
    {
        _logger = logger;
        _fileValidator = fileValidator;
        _fileSelectionCache = fileSelectionCache;
    }

    [HttpPost("capture-file-path")]
    public IActionResult CaptureFilePath(IFormFile file)
    {
        if (file == null)
        {
            return BadRequest(new { error = "No file provided" });
        }
        
        if (!_fileValidator.TryValidateUpload(file, MaxFileSize, out var error))
        {
            return BadRequest(new { error });
        }

        var safeName = _fileValidator.SanitizeFileName(file.FileName);
        _fileSelectionCache.Record(safeName);
        _logger.LogInformation("Captured file path: {FileName}", safeName);
        
        return Ok(new { path = safeName });
    }
    
    [HttpGet("last-file-path")]
    public IActionResult GetLastFilePath()
    {
        if (!_fileSelectionCache.TryGet(out var lastPath))
        {
            return NotFound(new { error = "No file has been uploaded yet" });
        }
        
        return Ok(new { path = lastPath });
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
                if (!_fileValidator.TryValidateExistingPath(path, out var validationError, out var normalizedPath))
                {
                    _logger.LogWarning("Native picker returned invalid path: {Reason}", validationError);
                    return BadRequest(new { error = validationError });
                }

                _fileSelectionCache.Record(normalizedPath);
                _logger.LogInformation("Native picker selected: {Path}", normalizedPath);
                return Ok(new { path = normalizedPath });
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
