using HopTracer.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
#if WINDOWS
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif

namespace HopTracer.Maui.Services;

/// <summary>
/// MAUI host for native pickers. File pick uses Essentials (cross-platform).
/// Folder pick uses platform APIs until Microsoft.Maui.Storage.FolderPicker ships widely —
/// keep platform branches behind #if so Mac Catalyst / macOS can plug in without rewriting callers.
/// </summary>
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
                        { DevicePlatform.macOS, new[] { "gh", "ghx" } },
                        { DevicePlatform.MacCatalyst, new[] { "gh", "ghx" } },
                        { DevicePlatform.iOS, new[] { "gh", "ghx" } },
                        { DevicePlatform.Android, new[] { "application/octet-stream", ".gh", ".ghx" } }
                    })
                };

                var result = await FilePicker.Default.PickAsync(options);
                if (result != null)
                {
                    _logger?.LogInformation("File selected: {Path}", result.FullPath);
                    return result.FullPath;
                }

                _logger?.LogInformation("File picker cancelled or no file selected");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in native file picker");
            }

            return null;
        });
    }

    public Task<string?> PickFolderAsync(string title) =>
        MainThread.InvokeOnMainThreadAsync(() => PickFolderCoreAsync(title));

    private async Task<string?> PickFolderCoreAsync(string title)
    {
        try
        {
            _logger?.LogInformation("Opening native folder picker: {Title}", title);

            // Prefer Essentials FolderPicker when the runtime exposes it (newer MAUI builds).
            var essentialsPath = await TryEssentialsFolderPickerAsync();
            if (essentialsPath != null)
            {
                return essentialsPath;
            }

#if WINDOWS
            return await PickFolderWindowsAsync();
#elif MACCATALYST || MACOS
            return await PickFolderAppleAsync(title);
#else
            // shortcut: no Essentials FolderPicker on this TFM yet; add platform branch when shipping that host
            _logger?.LogWarning("PickFolderAsync has no platform implementation for this target yet");
            return null;
#endif
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in native folder picker");
            return null;
        }
    }

    /// <summary>
    /// Reflection probe so we pick up Microsoft.Maui.Storage.FolderPicker without a hard dependency
    /// on an API that is not in every MAUI 10 build. Remove once FolderPicker is stable everywhere.
    /// </summary>
    private async Task<string?> TryEssentialsFolderPickerAsync()
    {
        try
        {
            var folderPickerType = Type.GetType("Microsoft.Maui.Storage.FolderPicker, Microsoft.Maui.Essentials")
                ?? Type.GetType("Microsoft.Maui.Storage.FolderPicker, Microsoft.Maui.Controls");
            if (folderPickerType is null)
            {
                return null;
            }

            var defaultProp = folderPickerType.GetProperty("Default", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var instance = defaultProp?.GetValue(null);
            if (instance is null)
            {
                return null;
            }

            var pick = instance.GetType().GetMethod("PickAsync", Type.EmptyTypes)
                ?? instance.GetType().GetMethods().FirstOrDefault(m => m.Name == "PickAsync" && m.GetParameters().Length <= 1);
            if (pick is null)
            {
                return null;
            }

            var args = pick.GetParameters().Length == 0 ? Array.Empty<object?>() : new object?[] { null };
            if (pick.Invoke(instance, args) is not Task task)
            {
                return null;
            }

            await task.ConfigureAwait(true);
            var resultProp = task.GetType().GetProperty("Result");
            var result = resultProp?.GetValue(task);
            if (result is null)
            {
                _logger?.LogInformation("Essentials folder picker cancelled");
                return null;
            }

            var path = result.GetType().GetProperty("Path")?.GetValue(result)?.ToString()
                ?? result.GetType().GetProperty("Folder")?.GetValue(result)
                    ?.GetType().GetProperty("Path")?.GetValue(result)?.ToString();

            if (!string.IsNullOrWhiteSpace(path))
            {
                _logger?.LogInformation("Folder selected via Essentials: {Path}", path);
                return path;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Essentials FolderPicker not usable; falling back to platform picker");
        }

        return null;
    }

#if WINDOWS
    private async Task<string?> PickFolderWindowsAsync()
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var window = Application.Current?.Windows?.FirstOrDefault();
        var hwnd = ((MauiWinUIWindow?)window?.Handler?.PlatformView)?.WindowHandle
            ?? throw new InvalidOperationException("No active MAUI window for folder picker.");
        InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            _logger?.LogInformation("Folder picker cancelled");
            return null;
        }

        _logger?.LogInformation("Folder selected: {Path}", folder.Path);
        return folder.Path;
    }
#endif

#if MACCATALYST || MACOS
    // Compiles when Mac TFMs are added to HopTracer.csproj.
    private static Task<string?> PickFolderAppleAsync(string title)
    {
        // NSOpenPanel CanChooseDirectories — wire when shipping the Mac host.
        // Keep the method so the shared PickFolderCoreAsync path stays valid.
        return Task.FromResult<string?>(null);
    }
#endif
}
