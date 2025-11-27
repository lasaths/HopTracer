namespace HopTracer.Core.Services;

public interface INativeIntegration
{
    Task<string?> PickFileAsync(string title);
}
