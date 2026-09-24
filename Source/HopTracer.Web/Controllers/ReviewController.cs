using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Serialization;
using HopTracer.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HopTracer.Web.Controllers;

[ApiController]
[Route("review")]
public class ReviewController : ControllerBase
{
    private readonly ILogger<ReviewController> _logger;
    private readonly IFileValidationService _fileValidator;
    private readonly IAppDataStorageService _storage;
    private readonly ITempFileManager _tempFiles;
    private readonly ReviewWorkflowService _review;

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
        _fileValidator = fileValidator;
        _storage = storage;
        _tempFiles = tempFiles;
        _review = new ReviewWorkflowService(parser, differ, converter, storage.BaselinesPath, logger: null);
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
            _ = tempScope;
            var baseline = _review.SaveBaseline(normalizedPath, request.Name);

            return Ok(new
            {
                ok = true,
                baseline = baseline.Name,
                baseline_file = Path.Combine(_storage.BaselinesPath, $"{SanitizeFileName(request.Name)}.json"),
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
            _ = tempScope;
            var result = _review.CompareBaseline(normalizedPath, request.Name);

            return Ok(new
            {
                baseline = result.Baseline.Name,
                fingerprint_match = result.FingerprintMatch,
                passed = result.Passed,
                risk_summary = result.Diff.RiskSummary,
                top_risks = result.Diff.TopRisks,
                diagnostics = result.Diff.Diagnostics
            });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
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
            _ = tempScope;
            var bundle = _review.BuildForensicReport(normalizedOld, normalizedNew, request.BaselineName);

            return Ok(new
            {
                report_json = bundle.ReportJson,
                report_html = bundle.ReportHtml,
                signature = bundle.Signature
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
                var baseline = _review.LoadBaseline(request.BaselineName);
                if (baseline != null)
                {
                    report.BaselineName = baseline.Name;
                    report.BaselineFingerprint = baseline.Fingerprint;
                }
            }

            var reportJson = JsonSerializer.Serialize(report);
            report.Signature = ComputeSha256(reportJson);
            reportJson = JsonSerializer.Serialize(report, AppJsonContext.Default.Options);

            return Ok(new
            {
                report_json = reportJson,
                report_html = BuildReportHtml(report),
                signature = report.Signature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build forensic report from diff payload");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string SanitizeFileName(string raw)
    {
        var chars = raw.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "baseline" : sanitized;
    }

    private static string ComputeSha256(string text)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
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
