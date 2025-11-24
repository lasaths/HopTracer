using System.IO;

namespace HopTracer.Maui;

public static class DependencyHelper
{
    public static void EnsureGhIoDll()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var dllPath = Path.Combine(appDir, "GH_IO.dll");

        if (File.Exists(dllPath))
        {
            System.Diagnostics.Debug.WriteLine($"GH_IO.dll found at {dllPath}");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"GH_IO.dll not found at {dllPath}, searching...");

        // List of paths to check (Newest Rhino first)
        var pathsToCheck = new[]
        {
            @"C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll",
            @"C:\Program Files\Rhino 7\Plug-ins\Grasshopper\GH_IO.dll",
            @"C:\Program Files\Rhino 6\Plug-ins\Grasshopper\GH_IO.dll"
        };

        foreach (var path in pathsToCheck)
        {
            if (File.Exists(path))
            {
                try
                {
                    File.Copy(path, dllPath);
                    System.Diagnostics.Debug.WriteLine($"Copied GH_IO.dll from {path}");
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to copy GH_IO.dll: {ex.Message}");
                }
            }
        }

        throw new FileNotFoundException("Could not find GH_IO.dll in standard Rhino installation paths. Please install Rhino 6, 7, or 8, or manually copy GH_IO.dll to the application directory.");
    }
}