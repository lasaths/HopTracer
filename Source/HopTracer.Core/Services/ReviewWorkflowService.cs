using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HopTracer.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HopTracer.Core.Services;

public class ReviewWorkflowService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IGhxParser _parser;
    private readonly IDiffer _differ;
    private readonly IConverterService _converter;
    private readonly string _baselinesPath;
    private readonly ILogger<ReviewWorkflowService> _logger;

    public ReviewWorkflowService(
        IGhxParser? parser = null,
        IDiffer? differ = null,
        IConverterService? converter = null,
        string? baselinesPath = null,
        ILogger<ReviewWorkflowService>? logger = null)
    {
        _parser = parser ?? new GhxParser(NullLogger<GhxParser>.Instance);
        _differ = differ ?? new Differ(NullLogger<Differ>.Instance);
        _converter = converter ?? new ConverterService(NullLogger<ConverterService>.Instance);
        _baselinesPath = baselinesPath ?? HopTracerPaths.BaselinesPath;
        _logger = logger ?? NullLogger<ReviewWorkflowService>.Instance;
        Directory.CreateDirectory(_baselinesPath);
    }

    public BaselineArtifact SaveBaseline(string sourcePath, string name)
    {
        var normalizedPath = Path.GetFullPath(sourcePath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException($"File not found: {normalizedPath}", normalizedPath);
        }

        var safeName = SanitizeName(name);
        var snapshotPath = Path.Combine(_baselinesPath, $"{safeName}.ghx");
        var metadataPath = Path.Combine(_baselinesPath, $"{safeName}.json");
        var temps = new List<string>();

        try
        {
            var preparedPath = PrepareGhx(normalizedPath, temps);
            File.Copy(preparedPath, snapshotPath, overwrite: true);

            var graph = _parser.Parse(snapshotPath);
            var baseline = new BaselineArtifact
            {
                Name = name,
                SourcePath = normalizedPath,
                SnapshotPath = snapshotPath,
                CreatedAt = DateTimeOffset.UtcNow,
                Fingerprint = ComputeFingerprint(graph),
                NodeCount = graph.Nodes.Count,
                EdgeCount = graph.Edges.Count
            };

            File.WriteAllText(metadataPath, JsonSerializer.Serialize(baseline, JsonOptions), Encoding.UTF8);
            return baseline;
        }
        finally
        {
            foreach (var temp in temps)
            {
                TryDelete(temp);
            }
        }
    }

    public IReadOnlyList<BaselineArtifact> ListBaselines()
    {
        if (!Directory.Exists(_baselinesPath))
        {
            return Array.Empty<BaselineArtifact>();
        }

        return Directory.EnumerateFiles(_baselinesPath, "*.json", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                try
                {
                    return JsonSerializer.Deserialize<BaselineArtifact>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Skipping invalid baseline metadata: {Path}", path);
                    return null;
                }
            })
            .Where(b => b != null)
            .Cast<BaselineArtifact>()
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    public BaselineCompareResult CompareBaseline(string currentPath, string baselineName)
    {
        var baseline = LoadBaseline(baselineName)
            ?? throw new InvalidOperationException($"Baseline '{baselineName}' not found.");

        if (!File.Exists(baseline.SnapshotPath))
        {
            throw new FileNotFoundException($"Baseline snapshot is missing: {baseline.SnapshotPath}");
        }

        var normalizedPath = Path.GetFullPath(currentPath);
        if (!File.Exists(normalizedPath))
        {
            throw new FileNotFoundException($"File not found: {normalizedPath}", normalizedPath);
        }

        var temps = new List<string>();
        try
        {
            var preparedCurrent = PrepareGhx(normalizedPath, temps);
            var oldGraph = _parser.Parse(baseline.SnapshotPath);
            var newGraph = _parser.Parse(preparedCurrent);
            var diff = _differ.DiffDetailed(oldGraph, newGraph);
            var currentFingerprint = ComputeFingerprint(newGraph);

            return new BaselineCompareResult
            {
                Baseline = baseline,
                Diff = diff,
                FingerprintMatch = string.Equals(baseline.Fingerprint, currentFingerprint, StringComparison.Ordinal),
                Passed = diff.RiskSummary.CriticalCount == 0 && diff.RiskSummary.HighCount == 0
            };
        }
        finally
        {
            foreach (var temp in temps)
            {
                TryDelete(temp);
            }
        }
    }

    public ForensicReportBundle BuildForensicReport(string oldPath, string newPath, string? baselineName = null)
    {
        var normalizedOld = Path.GetFullPath(oldPath);
        var normalizedNew = Path.GetFullPath(newPath);
        if (!File.Exists(normalizedOld))
        {
            throw new FileNotFoundException($"File not found: {normalizedOld}", normalizedOld);
        }
        if (!File.Exists(normalizedNew))
        {
            throw new FileNotFoundException($"File not found: {normalizedNew}", normalizedNew);
        }

        var temps = new List<string>();
        try
        {
            var preparedOld = PrepareGhx(normalizedOld, temps);
            var preparedNew = PrepareGhx(normalizedNew, temps);
            var diff = _differ.DiffDetailed(_parser.Parse(preparedOld), _parser.Parse(preparedNew));

            var report = new ForensicReportArtifact
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                FileOld = Path.GetFileName(normalizedOld),
                FileNew = Path.GetFileName(normalizedNew),
                NodeCount = diff.Nodes.Count,
                EdgeCount = diff.Edges.Count,
                RiskSummary = diff.RiskSummary,
                TopRisks = diff.TopRisks,
                Diagnostics = diff.Diagnostics
            };

            if (!string.IsNullOrWhiteSpace(baselineName))
            {
                var baseline = LoadBaseline(baselineName);
                if (baseline != null)
                {
                    report.BaselineName = baseline.Name;
                    report.BaselineFingerprint = baseline.Fingerprint;
                }
            }

            var unsignedJson = JsonSerializer.Serialize(report, JsonOptions);
            report.Signature = ComputeSha256(unsignedJson);
            var reportJson = JsonSerializer.Serialize(report, JsonOptions);
            var reportHtml = BuildReportHtml(report);
            return new ForensicReportBundle(reportJson, reportHtml, report.Signature);
        }
        finally
        {
            foreach (var temp in temps)
            {
                TryDelete(temp);
            }
        }
    }

    public DependencyCheckResult CheckDependencies()
    {
        var ghIoReady = _converter.TryCheckDependencies(out var ghIoMessage);
        return new DependencyCheckResult
        {
            GhIoAvailable = ghIoReady,
            GhIoMessage = ghIoMessage ?? string.Empty,
            BaselinesPath = _baselinesPath,
            Version = typeof(ReviewWorkflowService).Assembly.GetName().Version?.ToString(3) ?? "unknown"
        };
    }

    public BaselineArtifact? LoadBaseline(string name)
    {
        var metadataPath = Path.Combine(_baselinesPath, $"{SanitizeName(name)}.json");
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        return JsonSerializer.Deserialize<BaselineArtifact>(File.ReadAllText(metadataPath, Encoding.UTF8), JsonOptions);
    }

    private string PrepareGhx(string path, List<string> temps)
    {
        if (path.EndsWith(".ghx", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        if (!path.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unsupported file type: {Path.GetExtension(path)}");
        }

        if (!_converter.TryCheckDependencies(out var message))
        {
            throw new InvalidOperationException(message);
        }

        var tmp = Path.Combine(Path.GetTempPath(), $"hoptracer-{Guid.NewGuid():N}.ghx");
        temps.Add(tmp);
        return _converter.ConvertGhToGhx(path, tmp);
    }

    private static string ComputeFingerprint(Graph graph)
    {
        var nodeSignature = string.Join("\n",
            graph.Nodes.Values
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => $"{n.Id}|{n.Name}|{n.Nickname}|{n.X:F3}|{n.Y:F3}|{n.Inputs.Count}|{n.Outputs.Count}"));
        var edgeSignature = string.Join("\n",
            graph.Edges
                .OrderBy(e => e.Source, StringComparer.Ordinal)
                .ThenBy(e => e.SourcePort, StringComparer.Ordinal)
                .ThenBy(e => e.Target, StringComparer.Ordinal)
                .ThenBy(e => e.TargetPort, StringComparer.Ordinal)
                .Select(e => $"{e.Source}:{e.SourcePort}->{e.Target}:{e.TargetPort}"));
        return ComputeSha256(nodeSignature + "\n---\n" + edgeSignature);
    }

    private static string ComputeSha256(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private static string SanitizeName(string raw)
    {
        var chars = raw.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "baseline" : sanitized;
    }

    private static string BuildReportHtml(ForensicReportArtifact report)
    {
        var diagnostics = string.Join("", report.Diagnostics.Select(d =>
            $"<li><strong>{WebUtility.HtmlEncode(d.Severity.ToUpperInvariant())}</strong> [{WebUtility.HtmlEncode(d.Code)}] {WebUtility.HtmlEncode(d.Message)}</li>"));

        var risks = string.Join("", report.TopRisks.Select(r =>
            $"<li>{WebUtility.HtmlEncode(r.NodeName)} ({WebUtility.HtmlEncode(r.NodeStatus)}) - score {r.RiskScore}</li>"));

        return $"""
<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8"><title>HopTracer Forensic Report</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif; padding:24px;">
<h1>HopTracer Forensic Report</h1>
<p><strong>Generated:</strong> {report.GeneratedAt:O}</p>
<p><strong>Files:</strong> {WebUtility.HtmlEncode(report.FileOld)} → {WebUtility.HtmlEncode(report.FileNew)}</p>
<p><strong>Nodes:</strong> {report.NodeCount} | <strong>Edges:</strong> {report.EdgeCount}</p>
<p><strong>Risk:</strong> Critical {report.RiskSummary.CriticalCount}, High {report.RiskSummary.HighCount}, Medium {report.RiskSummary.MediumCount}, Low {report.RiskSummary.LowCount}</p>
<h2>Diagnostics</h2>
<ul>{diagnostics}</ul>
<h2>Top Risks</h2>
<ul>{risks}</ul>
<p><strong>Signature:</strong> <code>{report.Signature}</code></p>
</body>
</html>
""";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort temp cleanup
        }
    }
}

public sealed class BaselineCompareResult
{
    public required BaselineArtifact Baseline { get; init; }
    public required DiffComputation Diff { get; init; }
    public required bool FingerprintMatch { get; init; }
    public required bool Passed { get; init; }
}

public sealed class ForensicReportBundle
{
    public ForensicReportBundle(string reportJson, string reportHtml, string signature)
    {
        ReportJson = reportJson;
        ReportHtml = reportHtml;
        Signature = signature;
    }

    public string ReportJson { get; }
    public string ReportHtml { get; }
    public string Signature { get; }
}

public sealed class DependencyCheckResult
{
    public bool GhIoAvailable { get; init; }
    public string GhIoMessage { get; init; } = string.Empty;
    public string BaselinesPath { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
}
