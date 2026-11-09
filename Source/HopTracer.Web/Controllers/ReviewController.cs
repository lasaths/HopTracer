using HopTracer.Core.Models;
using HopTracer.Core.Services;
using HopTracer.Web.Services;
using Microsoft.AspNetCore.Mvc;

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
                AddedNodeCount = request.AddedNodeCount,
                RemovedNodeCount = request.RemovedNodeCount,
                ModifiedNodeCount = request.ModifiedNodeCount,
                AddedEdgeCount = request.AddedEdgeCount,
                RemovedEdgeCount = request.RemovedEdgeCount,
                RiskSummary = request.RiskSummary ?? new DiffRiskSummary(),
                TopRisks = request.TopRisks ?? new List<NodeRiskFinding>(),
                Diagnostics = request.Diagnostics ?? new List<DiffDiagnostic>(),
                ChangedNodes = request.ChangedNodes ?? new List<ForensicNodeChange>(),
                ChangedEdges = request.ChangedEdges ?? new List<ForensicEdgeChange>()
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

            var bundle = _review.BuildForensicReportFromArtifact(report);

            return Ok(new
            {
                report_json = bundle.ReportJson,
                report_html = bundle.ReportHtml,
                signature = bundle.Signature
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build forensic report from diff payload");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("report/write")]
    public IActionResult WriteForensicReport([FromBody] ForensicReportWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Folder) || !Directory.Exists(request.Folder))
        {
            return BadRequest(new { error = "A valid destination folder is required." });
        }

        if (string.IsNullOrWhiteSpace(request.ReportJson) || string.IsNullOrWhiteSpace(request.ReportHtml))
        {
            return BadRequest(new { error = "Report content is required." });
        }

        try
        {
            var stem = SanitizeFileName(string.IsNullOrWhiteSpace(request.Stem) ? $"hoptracer_forensic_{DateTime.Now:yyyyMMdd_HHmmss}" : request.Stem);
            var jsonPath = Path.Combine(request.Folder, $"{stem}.json");
            var htmlPath = Path.Combine(request.Folder, $"{stem}.html");
            System.IO.File.WriteAllText(jsonPath, request.ReportJson);
            System.IO.File.WriteAllText(htmlPath, request.ReportHtml);
            return Ok(new { ok = true, json = jsonPath, html = htmlPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write forensic report to {Folder}", request.Folder);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private static string SanitizeFileName(string raw)
    {
        var chars = raw.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray();
        var sanitized = new string(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "baseline" : sanitized;
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

public class ForensicReportWriteRequest
{
    public string Folder { get; set; } = string.Empty;
    public string? Stem { get; set; }
    public string ReportJson { get; set; } = string.Empty;
    public string ReportHtml { get; set; } = string.Empty;
}

public class ForensicReportFromDiffRequest
{
    public string? FileOld { get; set; }
    public string? FileNew { get; set; }
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public int AddedNodeCount { get; set; }
    public int RemovedNodeCount { get; set; }
    public int ModifiedNodeCount { get; set; }
    public int AddedEdgeCount { get; set; }
    public int RemovedEdgeCount { get; set; }
    public DiffRiskSummary? RiskSummary { get; set; }
    public List<NodeRiskFinding>? TopRisks { get; set; }
    public List<DiffDiagnostic>? Diagnostics { get; set; }
    public List<ForensicNodeChange>? ChangedNodes { get; set; }
    public List<ForensicEdgeChange>? ChangedEdges { get; set; }
    public string? BaselineName { get; set; }
}
