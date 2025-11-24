using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Services;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemController : ControllerBase
{
    private readonly INativeIntegration _native;

    public SystemController(INativeIntegration native)
    {
        _native = native;
    }

    [HttpPost("pick_file")]
    public async Task<IActionResult> PickFile()
    {
        var path = await _native.PickFileAsync("Select Grasshopper File");
        return Ok(new { path = path });
    }
}
