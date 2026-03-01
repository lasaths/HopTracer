using Microsoft.AspNetCore.Mvc;
using HopTracer.Core.Services;
using HopTracer.Web.Services;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class SystemController : ControllerBase
{
    private readonly INativeIntegration _native;
    private readonly IConverterService _converter;
    private readonly ISessionTokenService _sessionToken;

    public SystemController(INativeIntegration native, IConverterService converter, ISessionTokenService sessionToken)
    {
        _native = native;
        _converter = converter;
        _sessionToken = sessionToken;
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

    [HttpGet("session_token")]
    public IActionResult SessionToken()
    {
        return Ok(new { token = _sessionToken.Token });
    }
}
