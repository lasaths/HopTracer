using HopTracer.Core.Models;
using System.Text;
using System.Text.Json;

namespace HopTracer.Core.Services;

/// <summary>
/// Generates diff output in various formats (Text, Markdown, JSON, HTML).
/// </summary>
public class DiffOutputGenerator : IDiffOutputGenerator
{
    public string GenerateTextDiff(DiffComputation diff, DiffOutputOptions options)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("=" .PadRight(60, '='));
        sb.AppendLine("HOPTRACER DIFF REPORT");
        sb.AppendLine("=".PadRight(60, '='));

        if (!string.IsNullOrEmpty(options.FileOld) || !string.IsNullOrEmpty(options.FileNew))
        {
            sb.AppendLine($"Comparing: {options.FileOld ?? "Old"} → {options.FileNew ?? "New"}");
            sb.AppendLine();
        }

        // Statistics
        if (options.IncludeStatistics)
        {
            var stats = BuildStatistics(diff);
            sb.AppendLine("STATISTICS");
            sb.AppendLine("-".PadRight(40, '-'));
            sb.AppendLine($"Total nodes analyzed: {stats.TotalNodes}");
            sb.AppendLine($"Total edges analyzed: {stats.TotalEdges}");
            sb.AppendLine($"Added nodes: {stats.AddedNodes}");
            sb.AppendLine($"Removed nodes: {stats.RemovedNodes}");
            sb.AppendLine($"Modified nodes: {stats.ModifiedNodes}");
            sb.AppendLine($"Unchanged nodes: {stats.UnchangedNodes}");
            sb.AppendLine($"Added edges: {stats.AddedEdges}");
            sb.AppendLine($"Removed edges: {stats.RemovedEdges}");
            sb.AppendLine($"Changed connections: {stats.NodesWithConnectionChanges}");
            sb.AppendLine();
        }

        // Risk Summary
        if (options.IncludeRiskSummary)
        {
            sb.AppendLine("RISK SUMMARY");
            sb.AppendLine("-".PadRight(40, '-'));
            sb.AppendLine($"Critical (≥80): {diff.RiskSummary.CriticalCount}");
            sb.AppendLine($"High (60-79): {diff.RiskSummary.HighCount}");
            sb.AppendLine($"Medium (30-59): {diff.RiskSummary.MediumCount}");
            sb.AppendLine($"Low (1-29): {diff.RiskSummary.LowCount}");
            sb.AppendLine();
        }

        // Top Risks
        if (options.IncludeTopRisks && diff.TopRisks.Count > 0)
        {
            sb.AppendLine("TOP RISK ITEMS");
            sb.AppendLine("-".PadRight(40, '-'));
            foreach (var risk in diff.TopRisks)
            {
                var nodeId = options.CompactFormat ? risk.NodeId.Substring(0, Math.Min(8, risk.NodeId.Length)) : risk.NodeId;
                var nodeName = string.IsNullOrWhiteSpace(risk.NodeName) ? risk.NodeName : risk.NodeName;
                sb.AppendLine($"  [{risk.RiskScore:000}] {risk.NodeStatus}: {nodeName} ({nodeId})");
                if (!options.CompactFormat)
                {
                    foreach (var reason in risk.Reasons)
                    {
                        sb.AppendLine($"      - {reason}");
                    }
                }
            }
            sb.AppendLine();
        }

        // Changed Nodes
        if (options.IncludeChangedNodes)
        {
            var changedNodes = diff.Nodes
                .Where(n => n.Status != "same")
                .OrderByDescending(n => n.RiskScore)
                .ThenBy(n => n.Status)
                .ThenBy(n => n.Id)
                .Take(options.MaxNodesToShow)
                .ToList();

            if (changedNodes.Count > 0)
            {
                sb.AppendLine($"CHANGED NODES (showing {changedNodes.Count} of {diff.Nodes.Count(n => n.Status != "same")} total)");
                sb.AppendLine("-".PadRight(40, '-'));

                foreach (var node in changedNodes)
                {
                    var nodeId = options.CompactFormat ? node.Id.Substring(0, Math.Min(8, node.Id.Length)) : node.Id;
                    var nodeName = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
                    sb.AppendLine($"  [{node.Status.ToUpperInvariant()}] {nodeName} ({nodeId})");

                    if (options.IncludeNodeDetails)
                    {
                        sb.AppendLine($"      Position: ({node.X:F1}, {node.Y:F1})");
                        sb.AppendLine($"      Risk Score: {node.RiskScore}");

                        if (node.Dx != 0 || node.Dy != 0)
                        {
                            sb.AppendLine($"      Moved: ({node.Dx:+0.0}, {node.Dy:+0.0})");
                        }

                        if (node.PropertiesOld != null && node.PropertiesOld.Count > 0)
                        {
                            sb.AppendLine($"      Properties Changed:");
                            foreach (var prop in node.PropertiesOld.Keys.Take(5))
                            {
                                sb.AppendLine($"        - {prop}");
                            }
                        }

                        var changedPorts = node.Inputs.Concat(node.Outputs)
                            .Where(p => p.Status != "same" || p.ValueChanged || p.OptionsChanged)
                            .ToList();
                        if (changedPorts.Count > 0)
                        {
                            sb.AppendLine($"      Changed Ports ({changedPorts.Count}):");
                            foreach (var port in changedPorts.Take(3))
                            {
                                sb.AppendLine($"        - {port.Name} ({port.Kind}): {port.Status}");
                            }
                        }

                        if (node.InAdded > 0 || node.InRemoved > 0 || node.OutAdded > 0 || node.OutRemoved > 0)
                        {
                            sb.AppendLine($"      Connections: +{node.InAdded}/-{node.InRemoved} IN, +{node.OutAdded}/-{node.OutRemoved} OUT");
                        }
                    }
                    sb.AppendLine();
                }
            }
        }

        // Changed Edges
        if (options.IncludeChangedEdges)
        {
            var changedEdges = diff.Edges
                .Where(e => e.Status != "same")
                .OrderBy(e => e.Status)
                .ThenBy(e => e.Source)
                .Take(options.MaxEdgesToShow)
                .ToList();

            if (changedEdges.Count > 0)
            {
                sb.AppendLine($"CHANGED EDGES (showing {changedEdges.Count} of {diff.Edges.Count(e => e.Status != "same")} total)");
                sb.AppendLine("-".PadRight(40, '-'));

                foreach (var edge in changedEdges)
                {
                    var sourceId = options.CompactFormat ? edge.Source.Substring(0, Math.Min(8, edge.Source.Length)) : edge.Source;
                    var targetId = options.CompactFormat ? edge.Target.Substring(0, Math.Min(8, edge.Target.Length)) : edge.Target;
                    sb.AppendLine($"  [{edge.Status.ToUpperInvariant()}] {sourceId}:{edge.SourcePort} → {targetId}:{edge.TargetPort}");
                }
                sb.AppendLine();
            }
        }

        // Diagnostics
        if (options.IncludeDiagnostics && diff.Diagnostics.Count > 0)
        {
            sb.AppendLine("DIAGNOSTICS");
            sb.AppendLine("-".PadRight(40, '-'));
            foreach (var diag in diff.Diagnostics)
            {
                sb.AppendLine($"  [{diag.Severity.ToUpperInvariant()}] {diag.Message}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=" .PadRight(60, '='));
        return sb.ToString();
    }

    public string GenerateMarkdownDiff(DiffComputation diff, DiffOutputOptions options)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# HopTracer Diff Report");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(options.FileOld) || !string.IsNullOrEmpty(options.FileNew))
        {
            sb.AppendLine($"**Comparing:** `{options.FileOld ?? "Old"}` → `{options.FileNew ?? "New"}`");
            sb.AppendLine();
        }

        // Statistics
        if (options.IncludeStatistics)
        {
            var stats = BuildStatistics(diff);
            sb.AppendLine("## 📊 Statistics");
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|--------|-------|");
            sb.AppendLine($"| Total nodes analyzed | {stats.TotalNodes} |");
            sb.AppendLine($"| Total edges analyzed | {stats.TotalEdges} |");
            sb.AppendLine($"| Added nodes | {stats.AddedNodes} |");
            sb.AppendLine($"| Removed nodes | {stats.RemovedNodes} |");
            sb.AppendLine($"| Modified nodes | {stats.ModifiedNodes} |");
            sb.AppendLine($"| Unchanged nodes | {stats.UnchangedNodes} |");
            sb.AppendLine($"| Added edges | {stats.AddedEdges} |");
            sb.AppendLine($"| Removed edges | {stats.RemovedEdges} |");
            sb.AppendLine($"| Nodes with connection changes | {stats.NodesWithConnectionChanges} |");
            sb.AppendLine();
        }

        // Risk Summary
        if (options.IncludeRiskSummary)
        {
            sb.AppendLine("## 🎯 Risk Summary");
            sb.AppendLine($"- **Critical (≥80):** {diff.RiskSummary.CriticalCount}");
            sb.AppendLine($"- **High (60-79):** {diff.RiskSummary.HighCount}");
            sb.AppendLine($"- **Medium (30-59):** {diff.RiskSummary.MediumCount}");
            sb.AppendLine($"- **Low (1-29):** {diff.RiskSummary.LowCount}");
            sb.AppendLine();
        }

        // Top Risks
        if (options.IncludeTopRisks && diff.TopRisks.Count > 0)
        {
            sb.AppendLine("## ⚠️ Top Risk Items");
            sb.AppendLine();

            foreach (var risk in diff.TopRisks)
            {
                var emoji = risk.RiskScore >= 80 ? "🔴" : risk.RiskScore >= 60 ? "🟠" : risk.RiskScore >= 30 ? "🟡" : "🟢";
                sb.AppendLine($"{emoji} **[{risk.RiskScore:000}]** {risk.NodeStatus}: `{risk.NodeName}` ({risk.NodeId})");

                if (!options.CompactFormat)
                {
                    sb.AppendLine("  - Reasons:");
                    foreach (var reason in risk.Reasons)
                    {
                        sb.AppendLine($"    - {reason}");
                    }
                }
                sb.AppendLine();
            }
        }

        // Changed Nodes
        if (options.IncludeChangedNodes)
        {
            var changedNodes = diff.Nodes
                .Where(n => n.Status != "same")
                .OrderByDescending(n => n.RiskScore)
                .ThenBy(n => n.Status)
                .ThenBy(n => n.Id)
                .Take(options.MaxNodesToShow)
                .ToList();

            if (changedNodes.Count > 0)
            {
                sb.AppendLine($"## 🔧 Changed Nodes (showing {changedNodes.Count} of {diff.Nodes.Count(n => n.Status != "same")} total)");

                foreach (var node in changedNodes)
                {
                    var emoji = node.Status switch
                    {
                        "added" => "➕",
                        "removed" => "➖",
                        "modified" => "✏️",
                        _ => "📦"
                    };
                    var nodeName = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
                    sb.AppendLine();
                    sb.AppendLine($"{emoji} **{node.Status.ToUpperInvariant()}** `{nodeName}` (`{node.Id}`)");
                    sb.AppendLine($"  - Risk Score: **{node.RiskScore}**");

                    if (options.IncludeNodeDetails)
                    {
                        sb.AppendLine($"  - Position: `({node.X:F1}, {node.Y:F1})`");

                        if (node.Dx != 0 || node.Dy != 0)
                        {
                            sb.AppendLine($"  - Movement: `({node.Dx:+0.0}, {node.Dy:+0.0})`");
                        }

                        if (node.PropertiesOld != null && node.PropertiesOld.Count > 0)
                        {
                            sb.AppendLine("  - Properties Changed:");
                            foreach (var prop in node.PropertiesOld.Keys.Take(5))
                            {
                                sb.AppendLine($"    - `{prop}`");
                            }
                        }

                        var changedPorts = node.Inputs.Concat(node.Outputs)
                            .Where(p => p.Status != "same" || p.ValueChanged || p.OptionsChanged)
                            .ToList();
                        if (changedPorts.Count > 0)
                        {
                            sb.AppendLine($"  - Changed Ports ({changedPorts.Count}):");
                            foreach (var port in changedPorts.Take(3))
                            {
                                sb.AppendLine($"    - `{port.Name}` ({port.Kind}): {port.Status}");
                            }
                        }

                        if (node.InAdded > 0 || node.InRemoved > 0 || node.OutAdded > 0 || node.OutRemoved > 0)
                        {
                            sb.AppendLine($"  - Connections: `+{node.InAdded}/-{node.InRemoved}` IN, `+{node.OutAdded}/-{node.OutRemoved}` OUT");
                        }
                    }
                }

                if (diff.Nodes.Count(n => n.Status != "same") > options.MaxNodesToShow)
                {
                    var remaining = diff.Nodes.Count(n => n.Status != "same") - options.MaxNodesToShow;
                    sb.AppendLine();
                    sb.AppendLine($"_... and {remaining} more changed nodes_");
                }
                sb.AppendLine();
            }
        }

        // Changed Edges
        if (options.IncludeChangedEdges)
        {
            var changedEdges = diff.Edges
                .Where(e => e.Status != "same")
                .OrderBy(e => e.Status)
                .ThenBy(e => e.Source)
                .Take(options.MaxEdgesToShow)
                .ToList();

            if (changedEdges.Count > 0)
            {
                sb.AppendLine($"## 🔗 Changed Edges (showing {changedEdges.Count} of {diff.Edges.Count(e => e.Status != "same")} total)");
                sb.AppendLine();

                var edgeEmoji = new Dictionary<string, string>
                {
                    { "added", "➕" },
                    { "removed", "➖" },
                    { "same", "📦" }
                };

                foreach (var edge in changedEdges)
                {
                    var emoji = edgeEmoji.GetValueOrDefault(edge.Status, "📦");
                    sb.AppendLine($"{emoji} **{edge.Status.ToUpperInvariant()}** `{edge.Source}:{edge.SourcePort}` → `{edge.Target}:{edge.TargetPort}`");
                }

                if (diff.Edges.Count(e => e.Status != "same") > options.MaxEdgesToShow)
                {
                    var remaining = diff.Edges.Count(e => e.Status != "same") - options.MaxEdgesToShow;
                    sb.AppendLine();
                    sb.AppendLine($"_... and {remaining} more changed edges_");
                }
                sb.AppendLine();
            }
        }

        // Diagnostics
        if (options.IncludeDiagnostics && diff.Diagnostics.Count > 0)
        {
            sb.AppendLine("## 📋 Diagnostics");
            sb.AppendLine();

            foreach (var diag in diff.Diagnostics)
            {
                var emoji = diag.Severity switch
                {
                    "error" => "❌",
                    "warning" => "⚠️",
                    "info" => "ℹ️",
                    _ => "📌"
                };
                sb.AppendLine($"{emoji} **[{diag.Severity.ToUpperInvariant()}]** `{diag.Code}` - {diag.Message}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public string GenerateJsonDiff(DiffComputation diff, DiffOutputOptions options)
    {
        var result = new
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            FileOld = options.FileOld,
            FileNew = options.FileNew,
            Options = options,
            Summary = new
            {
                Statistics = BuildStatistics(diff),
                RiskSummary = diff.RiskSummary
            },
            TopRisks = diff.TopRisks,
            ChangedNodes = options.IncludeChangedNodes ? diff.Nodes
                .Where(n => n.Status != "same")
                .OrderByDescending(n => n.RiskScore)
                .ThenBy(n => n.Status)
                .ThenBy(n => n.Id)
                .Take(options.MaxNodesToShow)
                .ToList() : null,
            ChangedEdges = options.IncludeChangedEdges ? diff.Edges
                .Where(e => e.Status != "same")
                .OrderBy(e => e.Status)
                .ThenBy(e => e.Source)
                .Take(options.MaxEdgesToShow)
                .ToList() : null,
            Diagnostics = diff.Diagnostics
        };

        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(result, jsonOptions);
    }

    public string GenerateHtmlDiff(DiffComputation diff, DiffOutputOptions options)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>HopTracer Diff Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 24px; background: #f5f7fa; }");
        sb.AppendLine("        .container { max-width: 1200px; margin: 0 auto; background: white; padding: 24px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine("        h1, h2, h3 { color: #2c3e50; margin-top: 0; }");
        sb.AppendLine("        .header { border-bottom: 2px solid #3498db; padding-bottom: 16px; margin-bottom: 24px; }");
        sb.AppendLine("        .section { margin-bottom: 32px; }");
        sb.AppendLine("        .stats-table { width: 100%; border-collapse: collapse; margin: 16px 0; }");
        sb.AppendLine("        .stats-table th, .stats-table td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("        .stats-table th { background: #34495e; color: white; }");
        sb.AppendLine("        .risk-bar { display: flex; height: 24px; border-radius: 4px; overflow: hidden; margin: 8px 0; }");
        sb.AppendLine("        .risk-critical { background: #e74c3c; }");
        sb.AppendLine("        .risk-high { background: #f39c12; }");
        sb.AppendLine("        .risk-medium { background: #f1c40f; }");
        sb.AppendLine("        .risk-low { background: #27ae60; }");
        sb.AppendLine("        .node { padding: 12px; margin: 8px 0; border-left: 4px solid #3498db; background: #f8f9fa; border-radius: 4px; }");
        sb.AppendLine("        .node.added { border-left-color: #27ae60; }");
        sb.AppendLine("        .node.removed { border-left-color: #e74c3c; }");
        sb.AppendLine("        .node.modified { border-left-color: #f39c12; }");
        sb.AppendLine("        .node-header { display: flex; justify-content: space-between; align-items: center; }");
        sb.AppendLine("        .node-name { font-weight: bold; font-size: 16px; }");
        sb.AppendLine("        .node-status { padding: 4px 8px; border-radius: 4px; font-size: 12px; font-weight: bold; color: white; }");
        sb.AppendLine("        .status-added { background: #27ae60; }");
        sb.AppendLine("        .status-removed { background: #e74c3c; }");
        sb.AppendLine("        .status-modified { background: #f39c12; }");
        sb.AppendLine("        .risk-badge { background: #e74c3c; color: white; padding: 2px 8px; border-radius: 12px; font-size: 12px; font-weight: bold; }");
        sb.AppendLine("        .edge { padding: 8px 12px; margin: 4px 0; background: #f8f9fa; border-radius: 4px; display: flex; align-items: center; }");
        sb.AppendLine("        .edge.added { border-left: 3px solid #27ae60; }");
        sb.AppendLine("        .edge.removed { border-left: 3px solid #e74c3c; }");
        sb.AppendLine("        .diagnostic { padding: 8px 12px; margin: 8px 0; border-radius: 4px; }");
        sb.AppendLine("        .diagnostic.error { background: #fadbd8; border-left: 4px solid #e74c3c; }");
        sb.AppendLine("        .diagnostic.warning { background: #fef9e7; border-left: 4px solid #f39c12; }");
        sb.AppendLine("        .diagnostic.info { background: #ebf5fb; border-left: 4px solid #3498db; }");
        sb.AppendLine("        .node-details { margin-top: 8px; padding-left: 16px; font-size: 14px; color: #555; }");
        sb.AppendLine("        .pagination { text-align: center; color: #7f8c8d; font-style: italic; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");
        sb.AppendLine("        <div class=\"header\">");
        sb.AppendLine("            <h1>🔍 HopTracer Diff Report</h1>");
        sb.AppendLine($"            <p>Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");
        
        if (!string.IsNullOrEmpty(options.FileOld) || !string.IsNullOrEmpty(options.FileNew))
        {
            sb.AppendLine($"            <p><strong>Comparing:</strong> {System.Net.WebUtility.HtmlEncode(options.FileOld ?? "Old")} → {System.Net.WebUtility.HtmlEncode(options.FileNew ?? "New")}</p>");
        }
        
        sb.AppendLine("        </div>");

        // Statistics
        if (options.IncludeStatistics)
        {
            var stats = BuildStatistics(diff);
            sb.AppendLine("        <div class=\"section\">");
            sb.AppendLine("            <h2>📊 Statistics</h2>");
            sb.AppendLine("            <table class=\"stats-table\">");
            sb.AppendLine("                <tr><th>Metric</th><th>Value</th></tr>");
            sb.AppendLine($"                <tr><td>Total nodes analyzed</td><td>{stats.TotalNodes}</td></tr>");
            sb.AppendLine($"                <tr><td>Total edges analyzed</td><td>{stats.TotalEdges}</td></tr>");
            sb.AppendLine($"                <tr><td>Added nodes</td><td>{stats.AddedNodes}</td></tr>");
            sb.AppendLine($"                <tr><td>Removed nodes</td><td>{stats.RemovedNodes}</td></tr>");
            sb.AppendLine($"                <tr><td>Modified nodes</td><td>{stats.ModifiedNodes}</td></tr>");
            sb.AppendLine($"                <tr><td>Unchanged nodes</td><td>{stats.UnchangedNodes}</td></tr>");
            sb.AppendLine($"                <tr><td>Added edges</td><td>{stats.AddedEdges}</td></tr>");
            sb.AppendLine($"                <tr><td>Removed edges</td><td>{stats.RemovedEdges}</td></tr>");
            sb.AppendLine($"                <tr><td>Nodes with connection changes</td><td>{stats.NodesWithConnectionChanges}</td></tr>");
            sb.AppendLine("            </table>");
            sb.AppendLine("        </div>");
        }

        // Risk Summary
        if (options.IncludeRiskSummary)
        {
            var totalRisks = diff.RiskSummary.CriticalCount + diff.RiskSummary.HighCount + diff.RiskSummary.MediumCount + diff.RiskSummary.LowCount;
            var criticalPct = totalRisks > 0 ? (diff.RiskSummary.CriticalCount * 100.0 / totalRisks) : 0;
            var highPct = totalRisks > 0 ? (diff.RiskSummary.HighCount * 100.0 / totalRisks) : 0;
            var mediumPct = totalRisks > 0 ? (diff.RiskSummary.MediumCount * 100.0 / totalRisks) : 0;
            var lowPct = totalRisks > 0 ? (diff.RiskSummary.LowCount * 100.0 / totalRisks) : 0;

            sb.AppendLine("        <div class=\"section\">");
            sb.AppendLine("            <h2>🎯 Risk Summary</h2>");
            sb.AppendLine($"            <p>Critical: <strong>{diff.RiskSummary.CriticalCount}</strong> | ");
            sb.AppendLine($"High: <strong>{diff.RiskSummary.HighCount}</strong> | ");
            sb.AppendLine($"Medium: <strong>{diff.RiskSummary.MediumCount}</strong> | ");
            sb.AppendLine($"Low: <strong>{diff.RiskSummary.LowCount}</strong></p>");
            sb.AppendLine("            <div class=\"risk-bar\">");
            sb.AppendLine($"                <div class=\"risk-critical\" style=\"width: {criticalPct}%\" title=\"Critical: {diff.RiskSummary.CriticalCount}\"></div>");
            sb.AppendLine($"                <div class=\"risk-high\" style=\"width: {highPct}%\" title=\"High: {diff.RiskSummary.HighCount}\"></div>");
            sb.AppendLine($"                <div class=\"risk-medium\" style=\"width: {mediumPct}%\" title=\"Medium: {diff.RiskSummary.MediumCount}\"></div>");
            sb.AppendLine($"                <div class=\"risk-low\" style=\"width: {lowPct}%\" title=\"Low: {diff.RiskSummary.LowCount}\"></div>");
            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
        }

        // Top Risks
        if (options.IncludeTopRisks && diff.TopRisks.Count > 0)
        {
            sb.AppendLine("        <div class=\"section\">");
            sb.AppendLine("            <h2>⚠️ Top Risk Items</h2>");
            foreach (var risk in diff.TopRisks)
            {
                sb.AppendLine($"            <div class=\"node {risk.NodeStatus}\">");
                sb.AppendLine("                <div class=\"node-header\">");
                sb.AppendLine($"                    <span class=\"node-name\">{System.Net.WebUtility.HtmlEncode(risk.NodeName)}</span>");
                sb.AppendLine($"                    <span class=\"risk-badge\">{risk.RiskScore}</span>");
                sb.AppendLine("                </div>");
                sb.AppendLine($"                <div class=\"node-details\">");
                sb.AppendLine($"                    Status: <span class=\"node-status status-{risk.NodeStatus}\">{risk.NodeStatus.ToUpperInvariant()}</span><br>");
                sb.AppendLine($"                    ID: <code>{System.Net.WebUtility.HtmlEncode(risk.NodeId)}</code><br>");
                if (!options.CompactFormat)
                {
                    sb.AppendLine("                    Reasons:<br>");
                    foreach (var reason in risk.Reasons)
                    {
                        sb.AppendLine($"                    • {System.Net.WebUtility.HtmlEncode(reason)}<br>");
                    }
                }
                sb.AppendLine("                </div>");
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");
        }

        // Changed Nodes
        if (options.IncludeChangedNodes)
        {
            var changedNodes = diff.Nodes
                .Where(n => n.Status != "same")
                .OrderByDescending(n => n.RiskScore)
                .ThenBy(n => n.Status)
                .ThenBy(n => n.Id)
                .Take(options.MaxNodesToShow)
                .ToList();

            if (changedNodes.Count > 0)
            {
                var totalChanged = diff.Nodes.Count(n => n.Status != "same");
                sb.AppendLine($"        <div class=\"section\">");
                sb.AppendLine($"            <h2>🔧 Changed Nodes (showing {changedNodes.Count} of {totalChanged} total)</h2>");

                foreach (var node in changedNodes)
                {
                    var nodeName = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
                    sb.AppendLine($"            <div class=\"node {node.Status}\">");
                    sb.AppendLine("                <div class=\"node-header\">");
                    sb.AppendLine($"                    <span class=\"node-name\">{System.Net.WebUtility.HtmlEncode(nodeName)}</span>");
                    if (node.RiskScore > 0)
                    {
                        sb.AppendLine($"                    <span class=\"risk-badge\">{node.RiskScore}</span>");
                    }
                    sb.AppendLine("                </div>");
                    sb.AppendLine($"                <span class=\"node-status status-{node.Status}\">{node.Status.ToUpperInvariant()}</span>");

                    if (options.IncludeNodeDetails)
                    {
                        sb.AppendLine("                <div class=\"node-details\">");
                        sb.AppendLine($"                    <strong>ID:</strong> <code>{System.Net.WebUtility.HtmlEncode(node.Id)}</code><br>");
                        sb.AppendLine($"                    <strong>Position:</strong> ({node.X:F1}, {node.Y:F1})<br>");
                        if (node.Dx != 0 || node.Dy != 0)
                        {
                            sb.AppendLine($"                    <strong>Movement:</strong> ({node.Dx:+0.0}, {node.Dy:+0.0})<br>");
                        }
                        if (nodePropertiesChanged(node))
                        {
                            sb.AppendLine("                    <strong>Properties Changed:</strong><br>");
                            foreach (var prop in (node.PropertiesOld?.Keys ?? Enumerable.Empty<string>()).Take(5))
                            {
                                sb.AppendLine($"                    • {System.Net.WebUtility.HtmlEncode(prop)}<br>");
                            }
                        }
                        if (nodeHasConnectionChanges(node))
                        {
                            sb.AppendLine($"                    <strong>Connections:</strong> +{node.InAdded}/-{node.InRemoved} IN, +{node.OutAdded}/-{node.OutRemoved} OUT<br>");
                        }
                        var changedPorts = node.Inputs.Concat(node.Outputs)
                            .Where(p => p.Status != "same" || p.ValueChanged || p.OptionsChanged)
                            .ToList();
                        if (changedPorts.Count > 0)
                        {
                            sb.AppendLine($"                    <strong>Changed Ports ({changedPorts.Count}):</strong><br>");
                            foreach (var port in changedPorts.Take(3))
                            {
                                sb.AppendLine($"                    • {System.Net.WebUtility.HtmlEncode(port.Name)} ({port.Kind}): {port.Status}<br>");
                            }
                        }
                        sb.AppendLine("                </div>");
                    }
                    sb.AppendLine("            </div>");
                }

                if (totalChanged > options.MaxNodesToShow)
                {
                    var remaining = totalChanged - options.MaxNodesToShow;
                    sb.AppendLine($"            <p class=\"pagination\">... and {remaining} more changed nodes</p>");
                }
                sb.AppendLine("        </div>");
            }
        }

        // Changed Edges
        if (options.IncludeChangedEdges)
        {
            var changedEdges = diff.Edges
                .Where(e => e.Status != "same")
                .OrderBy(e => e.Status)
                .ThenBy(e => e.Source)
                .Take(options.MaxEdgesToShow)
                .ToList();

            if (changedEdges.Count > 0)
            {
                var totalChanged = diff.Edges.Count(e => e.Status != "same");
                sb.AppendLine($"        <div class=\"section\">");
                sb.AppendLine($"            <h2>🔗 Changed Edges (showing {changedEdges.Count} of {totalChanged} total)</h2>");

                foreach (var edge in changedEdges)
                {
                    sb.AppendLine($"            <div class=\"edge {edge.Status}\">");
                    sb.AppendLine($"                <span><strong>[{edge.Status.ToUpperInvariant()}]</strong> ");
                    sb.AppendLine($"{System.Net.WebUtility.HtmlEncode(edge.Source)}:{System.Net.WebUtility.HtmlEncode(edge.SourcePort)} → ");
                    sb.AppendLine($"{System.Net.WebUtility.HtmlEncode(edge.Target)}:{System.Net.WebUtility.HtmlEncode(edge.TargetPort)}</span>");
                    sb.AppendLine("            </div>");
                }

                if (totalChanged > options.MaxEdgesToShow)
                {
                    var remaining = totalChanged - options.MaxEdgesToShow;
                    sb.AppendLine($"            <p class=\"pagination\">... and {remaining} more changed edges</p>");
                }
                sb.AppendLine("        </div>");
            }
        }

        // Diagnostics
        if (options.IncludeDiagnostics && diff.Diagnostics.Count > 0)
        {
            sb.AppendLine("        <div class=\"section\">");
            sb.AppendLine("            <h2>📋 Diagnostics</h2>");
            foreach (var diag in diff.Diagnostics)
            {
                sb.AppendLine($"            <div class=\"diagnostic {diag.Severity}\">");
                sb.AppendLine($"                <strong>[{diag.Severity.ToUpperInvariant()}]</strong> <code>{System.Net.WebUtility.HtmlEncode(diag.Code)}</code> - {System.Net.WebUtility.HtmlEncode(diag.Message)}");
                sb.AppendLine("            </div>");
            }
            sb.AppendLine("        </div>");
        }

        sb.AppendLine("    </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    private static DiffStatistics BuildStatistics(DiffComputation diff)
    {
        var stats = new DiffStatistics
        {
            TotalNodes = diff.Nodes.Count,
            TotalEdges = diff.Edges.Count,
            AddedNodes = diff.Nodes.Count(n => n.Status == "added"),
            RemovedNodes = diff.Nodes.Count(n => n.Status == "removed"),
            ModifiedNodes = diff.Nodes.Count(n => n.Status == "modified"),
            UnchangedNodes = diff.Nodes.Count(n => n.Status == "same"),
            AddedEdges = diff.Edges.Count(e => e.Status == "added"),
            RemovedEdges = diff.Edges.Count(e => e.Status == "removed"),
            NodesWithConnectionChanges = diff.Nodes.Count(n =>
                n.InAdded > 0 || n.InRemoved > 0 || n.OutAdded > 0 || n.OutRemoved > 0)
        };

        return stats;
    }

    private static bool nodePropertiesChanged(Node node)
    {
        return node.PropertiesOld != null && node.PropertiesOld.Count > 0;
    }

    private static bool nodeHasConnectionChanges(Node node)
    {
        return node.InAdded > 0 || node.InRemoved > 0 || node.OutAdded > 0 || node.OutRemoved > 0;
    }
}

internal class DiffStatistics
{
    public int TotalNodes { get; set; }
    public int TotalEdges { get; set; }
    public int AddedNodes { get; set; }
    public int RemovedNodes { get; set; }
    public int ModifiedNodes { get; set; }
    public int UnchangedNodes { get; set; }
    public int AddedEdges { get; set; }
    public int RemovedEdges { get; set; }
    public int NodesWithConnectionChanges { get; set; }
}
