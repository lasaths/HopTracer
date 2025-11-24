using HopTracer.Core.Services;
using Microsoft.Maui.Storage;
using Microsoft.Maui.ApplicationModel;

namespace HopTracer.Maui.Services;

public class MauiNativeIntegration : INativeIntegration
{
    public async Task<string?> PickFileAsync(string title)
    {
        return await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
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
                    return result.FullPath;
                }
            }
            catch
            {
                // Ignore errors
            }
            return null;
        });
    }
}
