using Microsoft.Extensions.Logging;
using System.Reflection;

namespace HopTracer.Core.Services;

public class ConverterService : IConverterService
{
    private readonly ILogger<ConverterService> _logger;

    public ConverterService(ILogger<ConverterService> logger)
    {
        _logger = logger;
    }

    public string ConvertGhToGhx(string inputPath)
    {
        if (!TryCheckDependencies(out var dependencyMessage))
        {
            throw new InvalidOperationException(dependencyMessage);
        }

        if (!File.Exists(inputPath))
        {
            _logger.LogError("Input file not found: {Path}", inputPath);
            throw new FileNotFoundException("Input file not found", inputPath);
        }

        if (Path.GetExtension(inputPath).Equals(".ghx", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        var archiveType = ResolveGhArchiveType();
        if (archiveType == null)
        {
            throw new InvalidOperationException("GH_IO dependency unavailable. Run scripts/setup_dependencies.ps1 or ensure GH_IO.dll is available.");
        }

        var archive = Activator.CreateInstance(archiveType);
        if (archive == null)
        {
            throw new InvalidOperationException("Failed to initialize GH_IO archive.");
        }

        var readFromFile = archiveType.GetMethod("ReadFromFile", new[] { typeof(string) });
        var writeToFile = archiveType.GetMethod("WriteToFile", new[] { typeof(string), typeof(bool), typeof(bool) });
        if (readFromFile == null || writeToFile == null)
        {
            throw new InvalidOperationException("GH_IO archive API shape changed or is unavailable.");
        }

        var outputPath = Path.ChangeExtension(inputPath, ".ghx");
        _logger.LogInformation("Converting {InputPath} to {OutputPath}", inputPath, outputPath);

        var readOk = readFromFile.Invoke(archive, new object[] { inputPath }) is bool readResult && readResult;
        if (!readOk)
        {
            _logger.LogError("Failed to read GH file: {Path}", inputPath);
            throw new InvalidOperationException("Failed to read GH file. The file may be corrupted or unsupported.");
        }

        var writeOk = writeToFile.Invoke(archive, new object[] { outputPath, true, false }) is bool writeResult && writeResult;
        if (!writeOk)
        {
            _logger.LogError("Failed to write GHX file: {Path}", outputPath);
            throw new InvalidOperationException("Failed to write GHX file.");
        }

        return outputPath;
    }

    public bool TryCheckDependencies(out string message)
    {
        try
        {
            var archiveType = ResolveGhArchiveType();
            if (archiveType == null)
            {
                message = "GH_IO dependency unavailable. Run scripts/setup_dependencies.ps1 or place GH_IO.dll in the app directory.";
                return false;
            }

            var versionProp = archiveType.GetProperty("GH_IO_Version", BindingFlags.Public | BindingFlags.Static);
            var versionValue = versionProp?.GetValue(null)?.ToString();
            message = string.IsNullOrWhiteSpace(versionValue)
                ? "GH_IO ready"
                : $"GH_IO ready (version {versionValue})";
            return true;
        }
        catch (Exception ex)
        {
            message = $"GH_IO dependency unavailable: {ex.Message}. Ensure GH_IO.dll is accessible.";
            _logger.LogError(ex, "GH_IO dependency health check failed");
            return false;
        }
    }

    private static Type? ResolveGhArchiveType()
    {
        var resolved = Type.GetType("GH_IO.Serialization.GH_Archive, GH_IO", throwOnError: false);
        if (resolved != null)
        {
            return resolved;
        }

        var candidatePaths = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "GH_IO.dll"),
            Path.Combine(AppContext.BaseDirectory, "tools", "GH_IO.dll"),
            Path.Combine(AppContext.BaseDirectory, "HopTracer.Web", "tools", "GH_IO.dll"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Source", "HopTracer.Web", "tools", "GH_IO.dll")),
            // Add standard Rhino installation paths
            @"C:\Program Files\Rhino 8\Plug-ins\Grasshopper\GH_IO.dll",
            @"C:\Program Files\Rhino 7\Plug-ins\Grasshopper\GH_IO.dll",
            @"C:\Program Files\Rhino 6\Plug-ins\Grasshopper\GH_IO.dll"
        };

        foreach (var path in candidatePaths)
        {
            try
            {
                if (!File.Exists(path)) continue;
                var assembly = Assembly.LoadFrom(path);
                resolved = assembly.GetType("GH_IO.Serialization.GH_Archive", throwOnError: false, ignoreCase: false);
                if (resolved != null)
                {
                    return resolved;
                }
            }
            catch
            {
                // Optional dependency probing should not throw.
            }
        }

        return null;
    }
}
