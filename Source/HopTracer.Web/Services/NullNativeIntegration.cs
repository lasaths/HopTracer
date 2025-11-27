using HopTracer.Core.Services;

namespace HopTracer.Web.Services;

/// <summary>
/// Fallback native integration used when the web host runs standalone without MAUI.
/// It simply returns null so API endpoints can respond gracefully instead of failing DI resolution.
/// </summary>
public class NullNativeIntegration : INativeIntegration
{
    public Task<string?> PickFileAsync(string title) => Task.FromResult<string?>(null);
}
