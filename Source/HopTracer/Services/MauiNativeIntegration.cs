using HopTracer.Core.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.Logging;

namespace HopTracer.Maui.Services;

public class MauiNativeIntegration : INativeIntegration
{
    private readonly ILogger<MauiNativeIntegration>? _logger;

    public MauiNativeIntegration(ILogger<MauiNativeIntegration>? logger = null)
    {
        _logger = logger;
    }

    public async Task<string?> PickFileAsync(string title)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                _logger?.LogInformation("Opening native file picker: {Title}", title);
                
                var options = new PickOptions
                {
                    PickerTitle = title,
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".gh", ".ghx" } },
                        { DevicePlatform.macOS, new[] { "gh", "ghx" } }
                    })
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    _logger?.LogInformation("File selected: {Path}", result.FullPath);
                    return result.FullPath;
                }
                else
                {
                    _logger?.LogInformation("File picker cancelled or no file selected");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in native file picker");
            }
            return null;
        });
    }
}
