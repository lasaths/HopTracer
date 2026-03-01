using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Serialization;
using HopTracer.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("review")]
public class ReviewController : ControllerBase
{
    private readonly ILogger<ReviewController> _logger;
    private readonly IGhxParser _parser;
    private readonly IDiffer _differ;
    private readonly IConverterService _converter;
    private readonly IFileValidationService _fileValidator;
    private readonly IAppDataStorageService _storage;
    private readonly ITempFileManager _tempFiles;

    public ReviewController(
        ILogger<ReviewController> logger,
        IGhxParser parser,
        IDiffer differ,
        IConverterService converter,
        IFileValidationService fileValidator,
        IAppDataStorageService storage,
        ITempFileManager tempFiles)
    {
        _logger = logger;
        _parser = parser;
        _differ = differ;
        _converter = converter;
        _fileValidator = fileValidator;
        _storage = storage;
        _tempFiles = tempFiles;
    }

    [HttpPost("baseline/save")]
    public IActionResult SaveBaseline([FromBody] BaselineSaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Both path and name are required." });
        }

        if (!_fileValidator.TryValidateExistingPath(request.Path, out var error, out var normalizedPath))
        {
            return BadRequest(new { error });
        }

        try
        {
            using var tempScope = _tempFiles.CreateScope();

            var baselineDir = _storage.BaselinesPath;

            var safeName = SanitizeName(request.Name);
            var snapshotPath = Path.Combine(baselineDir, $"{safeName}.ghx");
            var metadataPath = Path.Combine(baselineDir, $"{safeName}.json");

            var preparedPath = EnsureGhx(normalizedPath, tempScope);
            System.IO.File.Copy(preparedPath, snapshotPath, overwrite: true);

            var graph = _parser.Parse(snapshotPath);
            var baseline = new BaselineArtifact
            {
                Name = request.Name,
                SourcePath = normalizedPath,
                SnapshotPath = snapshotPath,
                CreatedAt = DateTimeOffset.UtcNow,
                Fingerprint = ComputeFingerprint(graph),
                NodeCount = graph.Nodes.Count,
                EdgeCount = graph.Edges.Count
            };

            var json = JsonSerializer.Serialize(baseline);
            System.IO.File.WriteAllText(metadataPath, json, Encoding.UTF8);

            return Ok(new
            {
                ok = true,
                baseline = baseline.Name,
                baseline_file = metadataPath,
                node_count = baseline.NodeCount,
                edge_count = baseline.EdgeCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save baseline");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("baseline/compare")]
    public IActionResult CompareBaseline([FromBody] BaselineCompareRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Both path and name are required." });
        }

        if (!_fileValidator.TryValidateExistingPath(request.Path, out var error, out var normalizedPath))
        {
            return BadRequest(new { error });
        }

        try
        {
            using var tempScope = _tempFiles.CreateScope();

            var baseline = LoadBaseline(request.Name);
            if (baseline == null)
            {
                return NotFound(new { error = $"Baseline '{request.Name}' not found." });
            }

            if (!System.IO.File.Exists(baseline.SnapshotPath))
            {
                return NotFound(new { error = $"Baseline snapshot is missing: {baseline.SnapshotPath}" });
            }

            var currentPath = EnsureGhx(normalizedPath, tempScope);
            var oldGraph = _parser.Parse(baseline.SnapshotPath);
            var newGraph = _parser.Parse(currentPath);
            var diff = _differ.DiffDetailed(oldGraph, newGraph);

            var currentFingerprint = ComputeFingerprint(newGraph);
            var fingerprintMatch = string.Equals(baseline.Fingerprint, currentFingerprint, StringComparison.Ordinal);
            var hasCritical = diff.RiskSummary.CriticalCount > 0 || diff.RiskSummary.HighCount > 0;

            return Ok(new
            {
                baseline = baseline.Name,
                fingerprint_match = fingerprintMatch,
                passed = !hasCritical,
                risk_summary = diff.RiskSummary,
                top_risks = diff.TopRisks,
                diagnostics = diff.Diagnostics
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compare baseline");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("report/forensic")]
    public IActionResult ForensicReport([FromBody] ForensicReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PathOld) || string.IsNullOrWhiteSpace(request.PathNew))
        {
            return BadRequest(new { error = "pathOld and pathNew are required." });
        }

        if (!_fileValidator.TryValidateExistingPath(request.PathOld, out var oldError, out var normalizedOld))
        {
            return BadRequest(new { error = oldError });
        }

        if (!_fileValidator.TryValidateExistingPath(request.PathNew, out var newError, out var normalizedNew))
        {
            return BadRequest(new { error = newError });
        }

        try
        {
            using var tempScope = _tempFiles.CreateScope();
            var oldPath = EnsureGhx(normalizedOld, tempScope);
            var newPath = EnsureGhx(normalizedNew, tempScope);

            var oldGraph = _parser.Parse(oldPath);
            var newGraph = _parser.Parse(newPath);
            var diff = _differ.DiffDetailed(oldGraph, newGraph);

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

            if (!string.IsNullOrWhiteSpace(request.BaselineName))
            {
                var baseline = LoadBaseline(request.BaselineName);
                if (baseline != null)
                {
                    report.BaselineName = baseline.Name;
                    report.BaselineFingerprint = baseline.Fingerprint;
                }
            }

            var reportJson = JsonSerializer.Serialize(report);
            var signature = ComputeSha256(reportJson);
            report.Signature = signature;
            reportJson = JsonSerializer.Serialize(report, AppJsonContext.Default.Options);

            var reportHtml = BuildReportHtml(report);
            return Ok(new
            {
                report_json = reportJson,
                report_html = reportHtml,
                signature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build forensic report");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("report/from_diff")]
    public IActionResult ForensicReportFromDiff([FromBody] ForensicReportFromDiffRequest request)
    {
        try
        {
            var report = new ForensicReportArtifact
            {
                GeneratedAt = DateTimeOffset.UtcNow,
                FileOld = request.FileOld ?? "Old Version",
                FileNew = request.FileNew ?? "New Version",
                NodeCount = request.NodeCount,
                EdgeCount = request.EdgeCount,
                RiskSummary = request.RiskSummary ?? new DiffRiskSummary(),
                TopRisks = request.TopRisks ?? new List<NodeRiskFinding>(),
                Diagnostics = request.Diagnostics ?? new List<DiffDiagnostic>()
            };

            if (!string.IsNullOrWhiteSpace(request.BaselineName))
            {
                var baseline = LoadBaseline(request.BaselineName);
                if (baseline != null)
                {
                    report.BaselineName = baseline.Name;
                    report.BaselineFingerprint = baseline.Fingerprint;
                }
            }

            var reportJson = JsonSerializer.Serialize(report);
            var signature = ComputeSha256(reportJson);
            report.Signature = signature;
            reportJson = JsonSerializer.Serialize(report, AppJsonContext.Default.Options);

            var reportHtml = BuildReportHtml(report);
            return Ok(new
            {
                report_json = reportJson,
                report_html = reportHtml,
                signature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build forensic report from diff payload");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private string EnsureGhx(string normalizedPath, ITempFileScope tempScope)
    {
        var uploadsPath = _storage.UploadsPath;
        var extension = Path.GetExtension(normalizedPath);
        var tempSourcePath = Path.Combine(uploadsPath, $"review_{Guid.NewGuid():N}{extension}");
        System.IO.File.Copy(normalizedPath, tempSourcePath, overwrite: true);
        tempScope.Track(tempSourcePath);

        if (tempSourcePath.EndsWith(".ghx", StringComparison.OrdinalIgnoreCase))
        {
            return tempSourcePath;
        }

        if (!_converter.TryCheckDependencies(out var dependencyMessage))
        {
            throw new InvalidOperationException(dependencyMessage);
        }

        var convertedPath = _converter.ConvertGhToGhx(tempSourcePath);
        if (!string.Equals(convertedPath, tempSourcePath, StringComparison.OrdinalIgnoreCase))
        {
            tempScope.Track(convertedPath);
        }

        return convertedPath;
    }

    private BaselineArtifact? LoadBaseline(string name)
    {
        var safeName = SanitizeName(name);
        var metadataPath = Path.Combine(_storage.BaselinesPath, $"{safeName}.json");
        if (!System.IO.File.Exists(metadataPath))
        {
            return null;
        }

        var json = System.IO.File.ReadAllText(metadataPath, Encoding.UTF8);
        return JsonSerializer.Deserialize<BaselineArtifact>(json);
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
            $"<li><strong>{System.Net.WebUtility.HtmlEncode(d.Severity.ToUpperInvariant())}</strong> [{System.Net.WebUtility.HtmlEncode(d.Code)}] {System.Net.WebUtility.HtmlEncode(d.Message)}</li>"));

        var risks = string.Join("", report.TopRisks.Select(r =>
            $"<li>{System.Net.WebUtility.HtmlEncode(r.NodeName)} ({System.Net.WebUtility.HtmlEncode(r.NodeStatus)}) - score {r.RiskScore}</li>"));

        return $"""
<!DOCTYPE html>
<html lang="en">
<head><meta charset="utf-8"><title>HopTracer Forensic Report</title></head>
<body style="font-family:Segoe UI,Arial,sans-serif; padding:24px;">
<h1>HopTracer Forensic Report</h1>
<p><strong>Generated:</strong> {report.GeneratedAt:O}</p>
<p><strong>Files:</strong> {System.Net.WebUtility.HtmlEncode(report.FileOld)} → {System.Net.WebUtility.HtmlEncode(report.FileNew)}</p>
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
}

public class BaselineSaveRequest
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class BaselineCompareRequest
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class ForensicReportRequest
{
    public string PathOld { get; set; } = string.Empty;
    public string PathNew { get; set; } = string.Empty;
    public string? BaselineName { get; set; }
}

public class ForensicReportFromDiffRequest
{
    public string? FileOld { get; set; }
    public string? FileNew { get; set; }
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public DiffRiskSummary? RiskSummary { get; set; }
    public List<NodeRiskFinding>? TopRisks { get; set; }
    public List<DiffDiagnostic>? Diagnostics { get; set; }
    public string? BaselineName { get; set; }
}

public class BaselineArtifact
{
    public string Name { get; set; } = string.Empty;
    public string SourcePath { get; set; } = string.Empty;
    public string SnapshotPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
}

public class ForensicReportArtifact
{
    public DateTimeOffset GeneratedAt { get; set; }
    public string FileOld { get; set; } = string.Empty;
    public string FileNew { get; set; } = string.Empty;
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public DiffRiskSummary RiskSummary { get; set; } = new();
    public List<NodeRiskFinding> TopRisks { get; set; } = new();
    public List<DiffDiagnostic> Diagnostics { get; set; } = new();
    public string Signature { get; set; } = string.Empty;
    public string? BaselineName { get; set; }
    public string? BaselineFingerprint { get; set; }
}
