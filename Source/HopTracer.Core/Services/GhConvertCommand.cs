namespace HopTracer.Core.Services;

public enum GhConvertOverwriteDecision
{
    Overwrite,
    Skip,
    Cancel
}

public enum GhConvertFileStatus
{
    Converted,
    Skipped,
    Failed,
    Cancelled
}

public sealed record GhConvertFileResult(
    string InputPath,
    string OutputPath,
    GhConvertFileStatus Status,
    string Message);

public sealed class GhConvertBatchSummary
{
    public int RequestedCount { get; init; }
    public int ConvertedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public bool WasCancelled { get; init; }
    public IReadOnlyList<GhConvertFileResult> Results { get; init; } = Array.Empty<GhConvertFileResult>();
}

public sealed class GhConvertBatchRunner
{
    private readonly IConverterService _converter;

    public GhConvertBatchRunner(IConverterService converter)
    {
        _converter = converter;
    }

    public static bool TryParseConvertArguments(
        IReadOnlyList<string> args,
        out IReadOnlyList<string> inputPaths)
    {
        inputPaths = Array.Empty<string>();
        if (args.Count == 0)
        {
            return false;
        }

        if (!string.Equals(args[0], "--convert", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (args.Count > 1)
        {
            inputPaths = args
                .Skip(1)
                .Select(static p => (p ?? string.Empty).Trim())
                .Where(static p => p.Length > 0)
                .ToArray();
        }

        return true;
    }

    public GhConvertBatchSummary Run(
        IReadOnlyList<string> inputPaths,
        Func<string, string, GhConvertOverwriteDecision> overwritePrompt)
    {
        var results = new List<GhConvertFileResult>();
        var requested = inputPaths.Count;
        var wasCancelled = false;

        if (requested == 0)
        {
            return BuildSummary(requested, wasCancelled, results);
        }

        if (!_converter.TryCheckDependencies(out var dependencyMessage))
        {
            foreach (var inputPath in inputPaths)
            {
                var normalizedInput = NormalizeInputPath(inputPath);
                var outputPath = normalizedInput.Length == 0
                    ? string.Empty
                    : Path.ChangeExtension(normalizedInput, ".ghx");
                results.Add(new GhConvertFileResult(
                    normalizedInput,
                    outputPath,
                    GhConvertFileStatus.Failed,
                    dependencyMessage));
            }

            return BuildSummary(requested, wasCancelled, results);
        }

        foreach (var rawInputPath in inputPaths)
        {
            var inputPath = NormalizeInputPath(rawInputPath);
            var outputPath = inputPath.Length == 0
                ? string.Empty
                : Path.ChangeExtension(inputPath, ".ghx");

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                results.Add(new GhConvertFileResult(
                    inputPath,
                    outputPath,
                    GhConvertFileStatus.Failed,
                    "Input path is empty."));
                continue;
            }

            if (!Path.GetExtension(inputPath).Equals(".gh", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new GhConvertFileResult(
                    inputPath,
                    outputPath,
                    GhConvertFileStatus.Skipped,
                    "Skipped: only .gh files are supported."));
                continue;
            }

            if (!File.Exists(inputPath))
            {
                results.Add(new GhConvertFileResult(
                    inputPath,
                    outputPath,
                    GhConvertFileStatus.Failed,
                    "Input file not found."));
                continue;
            }

            if (File.Exists(outputPath))
            {
                var decision = overwritePrompt(inputPath, outputPath);
                if (decision == GhConvertOverwriteDecision.Skip)
                {
                    results.Add(new GhConvertFileResult(
                        inputPath,
                        outputPath,
                        GhConvertFileStatus.Skipped,
                        "Skipped existing output file."));
                    continue;
                }

                if (decision == GhConvertOverwriteDecision.Cancel)
                {
                    wasCancelled = true;
                    results.Add(new GhConvertFileResult(
                        inputPath,
                        outputPath,
                        GhConvertFileStatus.Cancelled,
                        "Batch cancelled by user."));
                    break;
                }
            }

            try
            {
                var convertedPath = _converter.ConvertGhToGhx(inputPath);
                results.Add(new GhConvertFileResult(
                    inputPath,
                    convertedPath,
                    GhConvertFileStatus.Converted,
                    "Converted successfully."));
            }
            catch (Exception ex)
            {
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Unknown conversion error."
                    : ex.Message.Trim();
                results.Add(new GhConvertFileResult(
                    inputPath,
                    outputPath,
                    GhConvertFileStatus.Failed,
                    message));
            }
        }

        return BuildSummary(requested, wasCancelled, results);
    }

    private static GhConvertBatchSummary BuildSummary(
        int requestedCount,
        bool wasCancelled,
        IReadOnlyList<GhConvertFileResult> results)
    {
        return new GhConvertBatchSummary
        {
            RequestedCount = requestedCount,
            ConvertedCount = results.Count(r => r.Status == GhConvertFileStatus.Converted),
            SkippedCount = results.Count(r => r.Status == GhConvertFileStatus.Skipped),
            FailedCount = results.Count(r => r.Status == GhConvertFileStatus.Failed),
            WasCancelled = wasCancelled || results.Any(r => r.Status == GhConvertFileStatus.Cancelled),
            Results = results
        };
    }

    private static string NormalizeInputPath(string rawInputPath)
    {
        var trimmed = (rawInputPath ?? string.Empty).Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        return trimmed;
    }
}
