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

            var report = BuildArtifactFromDiff(
                Path.GetFileName(normalizedOld),
                Path.GetFileName(normalizedNew),
                diff,
                baselineName);

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

    public ForensicReportBundle BuildForensicReportFromArtifact(ForensicReportArtifact report)
    {
        var unsignedJson = JsonSerializer.Serialize(report, JsonOptions);
        report.Signature = ComputeSha256(unsignedJson);
        var reportJson = JsonSerializer.Serialize(report, JsonOptions);
        return new ForensicReportBundle(reportJson, BuildReportHtml(report), report.Signature);
    }

    public ForensicReportArtifact BuildArtifactFromDiff(
        string fileOld,
        string fileNew,
        DiffComputation diff,
        string? baselineName = null,
        int maxNodes = 150,
        int maxEdges = 200)
    {
        var report = new ForensicReportArtifact
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            FileOld = fileOld,
            FileNew = fileNew,
            NodeCount = diff.Nodes.Count,
            EdgeCount = diff.Edges.Count,
            AddedNodeCount = diff.Nodes.Count(n => n.Status == "added"),
            RemovedNodeCount = diff.Nodes.Count(n => n.Status == "removed"),
            ModifiedNodeCount = diff.Nodes.Count(n => n.Status == "modified"),
            AddedEdgeCount = diff.Edges.Count(e => e.Status == "added"),
            RemovedEdgeCount = diff.Edges.Count(e => e.Status == "removed"),
            RiskSummary = diff.RiskSummary,
            TopRisks = diff.TopRisks,
            Diagnostics = diff.Diagnostics,
            ChangedNodes = BuildChangedNodes(diff, maxNodes),
            ChangedEdges = BuildChangedEdges(diff, maxEdges)
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

        return report;
    }

    private static List<ForensicNodeChange> BuildChangedNodes(DiffComputation diff, int maxNodes)
    {
        var labels = diff.Nodes.ToDictionary(
            n => n.Id,
            n => string.IsNullOrWhiteSpace(n.Nickname) ? n.Name : n.Nickname,
            StringComparer.Ordinal);

        string LabelOf(string id) => labels.TryGetValue(id, out var label) ? label : id;

        var upstream = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var downstream = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        void AddNeighbor(Dictionary<string, HashSet<string>> map, string nodeId, string neighbor, string edgeStatus)
        {
            if (!map.TryGetValue(nodeId, out var set))
            {
                set = new HashSet<string>(StringComparer.Ordinal);
                map[nodeId] = set;
            }

            var suffix = edgeStatus == "same" ? neighbor : $"{neighbor} [{edgeStatus}]";
            set.Add(suffix);
        }

        foreach (var edge in diff.Edges)
        {
            AddNeighbor(downstream, edge.Source, LabelOf(edge.Target), edge.Status);
            AddNeighbor(upstream, edge.Target, LabelOf(edge.Source), edge.Status);
        }

        return diff.Nodes
            .Where(n => n.Status != "same")
            .OrderByDescending(n => n.RiskScore)
            .ThenBy(n => n.Status, StringComparer.Ordinal)
            .ThenBy(n => n.Id, StringComparer.Ordinal)
            .Take(maxNodes)
            .Select(n =>
            {
                upstream.TryGetValue(n.Id, out var up);
                downstream.TryGetValue(n.Id, out var down);
                return new ForensicNodeChange
                {
                    NodeId = n.Id,
                    Label = LabelOf(n.Id),
                    ComponentName = n.Name,
                    Status = n.Status,
                    RiskScore = n.RiskScore,
                    Reasons = n.RiskReasons.ToList(),
                    InAdded = n.InAdded,
                    InRemoved = n.InRemoved,
                    OutAdded = n.OutAdded,
                    OutRemoved = n.OutRemoved,
                    Upstream = up?.OrderBy(x => x, StringComparer.Ordinal).ToList() ?? new List<string>(),
                    Downstream = down?.OrderBy(x => x, StringComparer.Ordinal).ToList() ?? new List<string>()
                };
            })
            .ToList();
    }

    private static List<ForensicEdgeChange> BuildChangedEdges(DiffComputation diff, int maxEdges)
    {
        var labels = diff.Nodes.ToDictionary(
            n => n.Id,
            n => string.IsNullOrWhiteSpace(n.Nickname) ? n.Name : n.Nickname,
            StringComparer.Ordinal);

        string LabelOf(string id) => labels.TryGetValue(id, out var label) ? label : id;

        return diff.Edges
            .Where(e => e.Status != "same")
            .OrderBy(e => e.Status, StringComparer.Ordinal)
            .ThenBy(e => e.Source, StringComparer.Ordinal)
            .ThenBy(e => e.Target, StringComparer.Ordinal)
            .Take(maxEdges)
            .Select(e => new ForensicEdgeChange
            {
                Status = e.Status,
                Source = e.Source,
                SourcePort = e.SourcePort,
                SourceLabel = LabelOf(e.Source),
                Target = e.Target,
                TargetPort = e.TargetPort,
                TargetLabel = LabelOf(e.Target)
            })
            .ToList();
    }

    public static string BuildReportHtml(ForensicReportArtifact report)
    {
        static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

        static string StatusClass(string status) => status switch
        {
            "added" => "added",
            "removed" => "removed",
            "modified" => "modified",
            _ => "same"
        };

        var diagnostics = report.Diagnostics.Count == 0
            ? "<p class=\"empty\">No diagnostics.</p>"
            : string.Join("", report.Diagnostics.Select(d =>
                $"<div class=\"diag {E(d.Severity)}\"><span class=\"pill\">{E(d.Severity)}</span> <code>{E(d.Code)}</code> {E(d.Message)}</div>"));

        var risks = report.TopRisks.Count == 0
            ? "<p class=\"empty\">No top risks.</p>"
            : string.Join("", report.TopRisks.Select(r =>
                $"<div class=\"card {StatusClass(r.NodeStatus)}\"><div class=\"card-head\"><span>{E(r.NodeName)}</span><span class=\"risk\">{r.RiskScore}</span></div><div class=\"meta\">{E(r.NodeStatus)} · {E(r.NodeId)}</div></div>"));

        var nodes = report.ChangedNodes.Count == 0
            ? "<p class=\"empty\">No changed components.</p>"
            : string.Join("", report.ChangedNodes.Select(n =>
            {
                var conn = (n.InAdded + n.InRemoved + n.OutAdded + n.OutRemoved) > 0
                    ? $"<div class=\"meta\">Connections: in +{n.InAdded}/-{n.InRemoved}, out +{n.OutAdded}/-{n.OutRemoved}</div>"
                    : "";
                var reasons = n.Reasons.Count == 0
                    ? ""
                    : $"<ul class=\"reasons\">{string.Join("", n.Reasons.Select(r => $"<li>{E(r)}</li>"))}</ul>";
                var up = n.Upstream.Count == 0
                    ? "<span class=\"muted\">none</span>"
                    : string.Join(", ", n.Upstream.Select(E));
                var down = n.Downstream.Count == 0
                    ? "<span class=\"muted\">none</span>"
                    : string.Join(", ", n.Downstream.Select(E));
                var riskBit = n.RiskScore > 0 ? $" · risk {n.RiskScore}" : "";
                return $"""
<div class="card {StatusClass(n.Status)}">
  <div class="card-head"><span>{E(n.Label)}</span><span class="pill {StatusClass(n.Status)}">{E(n.Status)}</span></div>
  <div class="meta">{E(n.ComponentName)} · {E(n.NodeId)}{riskBit}</div>
  {conn}
  {reasons}
  <div class="rels"><div><span class="label">Upstream</span> {up}</div><div><span class="label">Downstream</span> {down}</div></div>
</div>
""";
            }));

        var edges = report.ChangedEdges.Count == 0
            ? "<p class=\"empty\">No changed wires.</p>"
            : string.Join("", report.ChangedEdges.Select(e =>
                $"""
<div class="edge {StatusClass(e.Status)}">
  <span class="pill {StatusClass(e.Status)}">{E(e.Status)}</span>
  <span><strong>{E(e.SourceLabel)}</strong>:{E(e.SourcePort)} → <strong>{E(e.TargetLabel)}</strong>:{E(e.TargetPort)}</span>
</div>
"""));

        var baseline = string.IsNullOrWhiteSpace(report.BaselineName)
            ? ""
            : $"<div class=\"stat\"><span>Baseline</span><strong>{E(report.BaselineName)}</strong></div>";

        const string css = """
:root {
  --bg-app:#000; --bg-surface:#090A0C; --border:#3C3D41;
  --text:#DADADD; --text-secondary:rgba(218,218,221,.7); --muted:#8A8C91;
  --accent:#3A3CFF; --added:#44B782; --removed:#F45536; --modified:#F0963A;
  --font:-apple-system,BlinkMacSystemFont,'IBM Plex Sans',sans-serif;
  --mono:'IBM Plex Mono',Consolas,monospace;
}
* { box-sizing:border-box; }
body { margin:0; background:var(--bg-app); color:var(--text); font:13px/1.45 var(--font); }
.wrap { max-width:960px; margin:0 auto; padding:32px 24px 64px; }
.brand { color:var(--accent); font-weight:600; letter-spacing:.04em; text-transform:uppercase; font-size:11px; }
h1 { margin:8px 0 4px; font-size:28px; font-weight:500; }
.sub { color:var(--text-secondary); margin-bottom:24px; }
.grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(140px,1fr)); gap:8px; margin:16px 0 28px; }
.stat { background:var(--bg-surface); border:1px solid var(--border); border-radius:4px; padding:12px; }
.stat span { display:block; color:var(--muted); font-size:11px; margin-bottom:4px; }
.stat strong { font-size:18px; font-weight:500; }
h2 { font-size:14px; font-weight:600; letter-spacing:.03em; text-transform:uppercase; color:var(--muted); margin:28px 0 12px; border-bottom:1px solid var(--border); padding-bottom:8px; }
.card, .edge, .diag { background:var(--bg-surface); border:1px solid var(--border); border-radius:4px; padding:12px; margin:8px 0; }
.card.added, .edge.added { border-left:2px solid var(--added); }
.card.removed, .edge.removed { border-left:2px solid var(--removed); }
.card.modified { border-left:2px solid var(--modified); }
.card-head { display:flex; justify-content:space-between; gap:12px; align-items:center; font-weight:500; }
.meta, .rels { color:var(--text-secondary); margin-top:6px; font-size:12px; }
.rels { display:grid; gap:4px; }
.label { color:var(--muted); display:inline-block; min-width:88px; }
.reasons { margin:8px 0 0; padding-left:18px; color:var(--text-secondary); }
.pill { display:inline-block; padding:2px 8px; border-radius:4px; font-size:11px; text-transform:uppercase; background:rgba(138,140,145,.15); color:var(--muted); }
.pill.added { background:rgba(68,183,130,.15); color:var(--added); }
.pill.removed { background:rgba(244,85,54,.15); color:var(--removed); }
.pill.modified { background:rgba(240,150,58,.15); color:var(--modified); }
.risk { color:var(--removed); font-weight:600; }
.edge { display:flex; gap:10px; align-items:center; }
.diag.error { border-left:2px solid var(--removed); }
.diag.warning { border-left:2px solid var(--modified); }
.diag.info { border-left:2px solid var(--accent); }
.empty, .muted { color:var(--muted); }
code, .sig { font-family:var(--mono); font-size:12px; }
.sig { word-break:break-all; color:var(--text-secondary); margin-top:32px; padding-top:16px; border-top:1px solid var(--border); }
""";

        var generatedAt = report.GeneratedAt.ToString("yyyy-MM-dd HH:mm");
        return $"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>HopTracer Forensic Report</title>
<style>
{css}
</style>
</head>
<body>
<main class="wrap">
  <div class="brand">HopTracer</div>
  <h1>Forensic Report</h1>
  <p class="sub">{E(report.FileOld)} → {E(report.FileNew)} · {generatedAt} UTC</p>
  <div class="grid">
    <div class="stat"><span>Nodes</span><strong>{report.NodeCount}</strong></div>
    <div class="stat"><span>Wires</span><strong>{report.EdgeCount}</strong></div>
    <div class="stat"><span>Added / Removed / Modified</span><strong>{report.AddedNodeCount} / {report.RemovedNodeCount} / {report.ModifiedNodeCount}</strong></div>
    <div class="stat"><span>Wire +/-</span><strong>+{report.AddedEdgeCount} / -{report.RemovedEdgeCount}</strong></div>
    <div class="stat"><span>Risk C/H/M/L</span><strong>{report.RiskSummary.CriticalCount}/{report.RiskSummary.HighCount}/{report.RiskSummary.MediumCount}/{report.RiskSummary.LowCount}</strong></div>
    {baseline}
  </div>
  <h2>Changed Components</h2>
  {nodes}
  <h2>Changed Relationships</h2>
  {edges}
  <h2>Top Risks</h2>
  {risks}
  <h2>Diagnostics</h2>
  {diagnostics}
  <p class="sig">Signature {E(report.Signature)}</p>
</main>
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
