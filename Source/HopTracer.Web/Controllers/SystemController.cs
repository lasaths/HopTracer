using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Services;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemController : ControllerBase
{
    private readonly INativeIntegration _native;
    private readonly IConverterService _converter;

    public SystemController(INativeIntegration native, IConverterService converter)
    {
        _native = native;
        _converter = converter;
    }

    [HttpPost("pick_file")]
    public async Task<IActionResult> PickFile()
    {
        var path = await _native.PickFileAsync("Select Grasshopper File");
        return Ok(new { path = path });
    }

    [HttpGet("dependency_health")]
    public IActionResult DependencyHealth()
    {
        var ok = _converter.TryCheckDependencies(out var message);
        return Ok(new { ok, message });
    }
}
