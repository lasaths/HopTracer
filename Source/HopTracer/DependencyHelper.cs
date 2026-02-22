using System.Diagnostics;
using System.IO;

namespace HopTracer.Maui;

public static class DependencyHelper
{
    /// <summary>
    /// Ensures GH_IO.dll is present (and up to date) in all runtime locations.
    /// Called on app startup so deployment always refreshes the Rhino DLL.
    /// </summary>
    public static void EnsureGhIoDll()
    {
        if (!TryEnsureGhIoDll(out var errorMessage))
        {
            throw new FileNotFoundException(errorMessage);
        }
    }

    /// <summary>
    /// Ensures GH_IO.dll is present (and up to date) in all runtime locations.
    /// Returns false with a detailed error message instead of throwing.
    /// </summary>
    public static bool TryEnsureGhIoDll(out string? errorMessage)
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;

        // Prioritize newer Rhino installs first
        var candidatePaths = new[]
        {
            @"C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll",
            @"C:\Program Files\Rhino 7\Plug-ins\Grasshopper\GH_IO.dll",
            @"C:\Program Files\Rhino 6\Plug-ins\Grasshopper\GH_IO.dll"
        };

        var sourcePath = FindNewestGhIo(candidatePaths);
        if (sourcePath is null)
        {
            errorMessage =
                "GH_IO.dll was not found. HopTracer requires Grasshopper's GH_IO.dll at startup.\n" +
                "Install Rhino 8/7/6, then retry.\n" +
                "Expected source paths:\n" +
                "- C:\\Program Files\\Rhino 8\\Plug-ins\\Grasshopper\\GH_IO.dll\n" +
                "- C:\\Program Files\\Rhino 7\\Plug-ins\\Grasshopper\\GH_IO.dll\n" +
                "- C:\\Program Files\\Rhino 6\\Plug-ins\\Grasshopper\\GH_IO.dll\n" +
                "For source builds, run .\\scripts\\setup_dependencies.ps1.";
            return false;
        }

        var targets = new[]
        {
            appDir,
            Path.Combine(appDir, "tools"),
            Path.Combine(appDir, "GhConverter"),
            Path.Combine(appDir, "GhConverter", "publish")
        };

        foreach (var targetDir in targets)
        {
            CopyIfNewer(sourcePath, targetDir);
        }

        errorMessage = null;
        return true;
    }

    private static string? FindNewestGhIo(IEnumerable<string> candidatePaths)
    {
        string? bestPath = null;
        Version? bestVersion = null;

        foreach (var path in candidatePaths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                var ver = FileVersionInfo.GetVersionInfo(path).FileVersion is string v
                    ? new Version(v)
                    : null;

                if (ver != null && (bestVersion == null || ver > bestVersion))
                {
                    bestVersion = ver;
                    bestPath = path;
                }
            }
            catch
            {
                // Ignore unreadable versions and continue
            }
        }

        if (bestPath != null)
        {
            Debug.WriteLine($"Using GH_IO.dll from {bestPath} (v{bestVersion})");
        }
        return bestPath;
    }

    private static void CopyIfNewer(string sourcePath, string targetDir)
    {
        if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
        {
            return; // Skip non-existent targets
        }

        var targetPath = Path.Combine(targetDir, "GH_IO.dll");
        try
        {
            var shouldCopy = true;

            if (File.Exists(targetPath))
            {
                var srcVer = FileVersionInfo.GetVersionInfo(sourcePath).FileVersion;
                var dstVer = FileVersionInfo.GetVersionInfo(targetPath).FileVersion;
                if (srcVer != null && dstVer != null && Version.TryParse(srcVer, out var sv) && Version.TryParse(dstVer, out var dv))
                {
                    shouldCopy = sv > dv;
                }
                else
                {
                    shouldCopy = true;
                }
            }

            if (shouldCopy)
            {
                File.Copy(sourcePath, targetPath, true);
                Debug.WriteLine($"Copied GH_IO.dll to {targetPath}");
            }
            else
            {
                Debug.WriteLine($"GH_IO.dll already up to date at {targetPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to copy GH_IO.dll to {targetPath}: {ex.Message}");
        }
    }
}
