using GH_IO.Serialization;
using Microsoft.Extensions.Logging;

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
        if (!File.Exists(inputPath))
        {
            _logger.LogError("Input file not found: {Path}", inputPath);
            throw new FileNotFoundException("Input file not found", inputPath);
        }

        // If it's already a .ghx file, just return the path
        if (Path.GetExtension(inputPath).Equals(".ghx", StringComparison.OrdinalIgnoreCase))
        {
            return inputPath;
        }

        string outputPath = Path.ChangeExtension(inputPath, ".ghx");
        _logger.LogInformation("Converting {InputPath} to {OutputPath}", inputPath, outputPath);

        var archive = new GH_Archive();
        if (!archive.ReadFromFile(inputPath))
        {
            _logger.LogError("Failed to read GH file: {Path}", inputPath);
            throw new InvalidOperationException("Failed to read GH file. The file might be corrupted or in an unsupported format.");
        }

        if (!archive.WriteToFile(outputPath, true, false)) // true = xml, false = binary
        {
            _logger.LogError("Failed to write GHX file: {Path}", outputPath);
            throw new InvalidOperationException("Failed to write GHX file.");
        }

        return outputPath;
    }
}