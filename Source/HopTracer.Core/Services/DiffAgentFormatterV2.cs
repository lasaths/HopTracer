using HopTracer.Core.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HopTracer.Core.Services;

public static class DiffAgentFormatterV2
{
    private const int MaxChangesToShow = 100;
    private const int ScriptDiffContextLines = 3;

    public static string Generate(DiffComputation diff, DiffOutputOptions options)
    {
        var resolver = new DiffGraphResolver(diff.Nodes);
        
        // Count cosmetic moves: nodes that only moved position, no other changes
        var cosmeticMoves = diff.Nodes.Count(n => 
            n.Status == "same" && 
            n.RiskScore <= 10 && 
            (n.PropertiesOld == null || n.PropertiesOld.Count == 0) && 
            (n.Dx != 0 || n.Dy != 0));
        
        // Significant changes exclude cosmetic-only moves
        var significantChanges = diff.Nodes
            .Where(n => n.Status != "same" || n.RiskScore > 10 || (n.PropertiesOld != null && n.PropertiesOld.Count > 0))
            .OrderByDescending(n => n.RiskScore)
            .ThenBy(n => n.Status)
            .ThenBy(n => n.Id)
            .ToList();
        
        var displayedChanges = significantChanges.Take(MaxChangesToShow).ToList();
        var omittedCount = Math.Max(0, significantChanges.Count - MaxChangesToShow);
        
        var changes = displayedChanges.Select(node => BuildChange(node, resolver, diff)).ToList();
        var wires = BuildWireChanges(diff, resolver);
        
        var result = new AgentDiffReportV2
        {
            Schema = "hoptracer.agent/2",
            GeneratedAt = options.IncludeTimestamp ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
            Inputs = new InputFiles
            {
                Old = options.FileOld ?? "old",
                New = options.FileNew ?? "new",
                OldSha256 = ComputeFileHash(options.FileOld),
                NewSha256 = ComputeFileHash(options.FileNew)
            },
            Summary = BuildSummaryV2(diff, changes, cosmeticMoves),
            Changes = changes,
            Wires = wires,
            Cosmetic = new CosmeticChanges { Moved = cosmeticMoves },
            Diagnostics = BuildDiagnosticsV2(diff.Diagnostics),
            Truncation = new TruncationInfo
            {
                Listed = displayedChanges.Count,
                Total = significantChanges.Count,
                OmittedLowRisk = omittedCount
            }
        };

        return JsonSerializer.Serialize(result, AgentJsonOptions);
    }

    private static ChangeItem BuildChange(Node node, DiffGraphResolver resolver, DiffComputation diff)
    {
        var nodeLabel = string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;
        var change = new ChangeItem
        {
            Id = node.Id.Length > 8 ? node.Id.Substring(0, 8) : node.Id,
            Name = nodeLabel,
            Type = node.Name,
            Path = new[] { "root" },
            Status = node.Status,
            Risk = new RiskInfo
            {
                Score = node.RiskScore,
                Level = GetRiskLevel(node.RiskScore),
                Reasons = node.RiskReasons.Any() ? node.RiskReasons : new List<string> { "Node modified" }
            },
            Impact = BuildImpact(node, diff)
        };

        // Check for name/nickname changes
        var rename = TryGetRename(node);
        if (rename != null)
        {
            change.Rename = rename;
        }

        var propertyChanges = GetActualPropertyChanges(node);
        if (propertyChanges.Count > 0)
        {
            change.Params = propertyChanges.Select(kvp => new ParamChange
            {
                Key = kvp.Key,
                Old = kvp.Value.Old,
                New = kvp.Value.New
            }).ToList();
        }

        var scriptChange = TryGetScriptChange(node);
        if (scriptChange != null)
        {
            change.Script = scriptChange;
        }

        return change;
    }

    private static NameChange? TryGetRename(Node node)
    {
        if (node.PropertiesOld == null)
        {
            return null;
        }

        node.PropertiesOld.TryGetValue("Name", out var oldName);
        node.Properties.TryGetValue("Name", out var newName);
        node.PropertiesOld.TryGetValue("NickName", out var oldNickname);
        node.Properties.TryGetValue("NickName", out var newNickname);

        var hasNameChange = !string.IsNullOrEmpty(oldName) && !string.IsNullOrEmpty(newName) && oldName != newName;
        var hasNicknameChange = !string.IsNullOrEmpty(oldNickname) && !string.IsNullOrEmpty(newNickname) && oldNickname != newNickname;

        if (hasNameChange || hasNicknameChange)
        {
            return new NameChange
            {
                OldName = hasNameChange ? oldName : null,
                NewName = hasNameChange ? newName : null,
                OldNickname = hasNicknameChange ? oldNickname : null,
                NewNickname = hasNicknameChange ? newNickname : null
            };
        }

        return null;
    }

    private static List<WireChange> BuildWireChanges(DiffComputation diff, DiffGraphResolver resolver)
    {
        return diff.Edges
            .Where(e => e.Status != "same")
            .OrderBy(e => e.Status)
            .ThenBy(e => e.Source)
            .Take(50)
            .Select(e =>
            {
                var resolved = resolver.Resolve(e);
                return new WireChange
                {
                    Status = e.Status,
                    From = resolved.From,
                    To = resolved.To,
                    Wire = resolved.Wire
                };
            })
            .ToList();
    }

    private static Dictionary<string, (string Old, string New)> GetActualPropertyChanges(Node node)
    {
        var changes = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        
        if (node.PropertiesOld == null || node.PropertiesOld.Count == 0)
        {
            return changes;
        }

        // Check if Text is a base64-encoded script duplicate
        node.Properties.TryGetValue("ScriptSource", out var scriptSource);
        node.Properties.TryGetValue("Text", out var textProp);
        var textIsScriptDuplicate = !string.IsNullOrEmpty(scriptSource) && 
                                    !string.IsNullOrEmpty(textProp) && 
                                    textProp.Length > 100 &&
                                    textProp.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=');

        foreach (var key in node.PropertiesOld.Keys)
        {
            if (ShouldSkipProperty(key))
            {
                continue;
            }

            // Skip Text only if it's a script duplicate
            if (key.Equals("Text", StringComparison.OrdinalIgnoreCase) && textIsScriptDuplicate)
            {
                continue;
            }

            node.PropertiesOld.TryGetValue(key, out var oldValue);
            node.Properties.TryGetValue(key, out var newValue);
            oldValue ??= string.Empty;
            newValue ??= string.Empty;

            if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsScriptProperty(key) || IsOpaqueProperty(key, oldValue, newValue))
            {
                continue;
            }

            changes[key] = (oldValue, newValue);
        }

        return changes;
    }

    private static ScriptChange? TryGetScriptChange(Node node)
    {
        if (node.PropertiesOld == null)
        {
            return null;
        }

        foreach (var key in node.PropertiesOld.Keys)
        {
            if (!IsScriptProperty(key))
            {
                continue;
            }

            node.PropertiesOld.TryGetValue(key, out var oldScript);
            node.Properties.TryGetValue(key, out var newScript);
            oldScript ??= string.Empty;
            newScript ??= string.Empty;

            if (string.Equals(oldScript, newScript, StringComparison.Ordinal))
            {
                continue;
            }

            var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV2.Generate(oldScript, newScript);

            return new ScriptChange
            {
                Language = InferLanguage(key, node.Name),
                LinesAdded = added,
                LinesRemoved = removed,
                UnifiedDiff = diff,
                Truncated = truncated ? true : null,
                LineEndingChange = lineEndingChange
            };
        }

        return null;
    }

    private static ImpactInfo BuildImpact(Node node, DiffComputation diff)
    {
        var upstreamNodes = new List<ImpactNode>();
        var downstreamNodes = new List<ImpactNode>();

        // Build a resolver to get port names
        var resolver = new DiffGraphResolver(diff.Nodes);

        foreach (var edge in diff.Edges.Where(e => e.Target == node.Id))
        {
            var sourceNode = diff.Nodes.FirstOrDefault(n => n.Id == edge.Source);
            if (sourceNode != null)
            {
                var label = string.IsNullOrWhiteSpace(sourceNode.Nickname) ? sourceNode.Name : sourceNode.Nickname;
                var resolved = resolver.Resolve(edge);
                upstreamNodes.Add(new ImpactNode
                {
                    Id = sourceNode.Id.Length > 8 ? sourceNode.Id.Substring(0, 8) : sourceNode.Id,
                    Name = label,
                    Port = resolved.TargetPortName,
                    Status = edge.Status != "same" ? edge.Status : null
                });
            }
        }

        foreach (var edge in diff.Edges.Where(e => e.Source == node.Id))
        {
            var targetNode = diff.Nodes.FirstOrDefault(n => n.Id == edge.Target);
            if (targetNode != null)
            {
                var label = string.IsNullOrWhiteSpace(targetNode.Nickname) ? targetNode.Name : targetNode.Nickname;
                var resolved = resolver.Resolve(edge);
                downstreamNodes.Add(new ImpactNode
                {
                    Id = targetNode.Id.Length > 8 ? targetNode.Id.Substring(0, 8) : targetNode.Id,
                    Name = label,
                    Port = resolved.SourcePortName,
                    Status = edge.Status != "same" ? edge.Status : null
                });
            }
        }

        return new ImpactInfo
        {
            Upstream = upstreamNodes,
            Downstream = downstreamNodes,
            DownstreamCount = downstreamNodes.Count
        };
    }

    private static SummaryInfo BuildSummaryV2(DiffComputation diff, List<ChangeItem> changes, int cosmeticMoves)
    {
        var stats = diff.Nodes.Where(n => n.Status != "same").ToList();
        var added = stats.Count(n => n.Status == "added");
        var removed = stats.Count(n => n.Status == "removed");
        var modified = stats.Count(n => n.Status == "modified");
        
        var wiresChanged = diff.Edges.Where(e => e.Status != "same").ToList();
        var wiresAdded = wiresChanged.Count(e => e.Status == "added");
        var wiresRemoved = wiresChanged.Count(e => e.Status == "removed");
        
        var maxRisk = changes.Any() ? changes.Max(c => c.Risk.Score) : 0;
        var maxRiskLevel = GetRiskLevel(maxRisk);

        var headline = BuildHeadline(changes, added, removed, modified);

        return new SummaryInfo
        {
            Added = added,
            Removed = removed,
            Modified = modified,
            WiresAdded = wiresAdded,
            WiresRemoved = wiresRemoved,
            MaxRisk = maxRiskLevel,
            Headline = headline
        };
    }

    private static string BuildHeadline(List<ChangeItem> changes, int added, int removed, int modified)
    {
        var parts = new List<string>();

        if (added > 0)
        {
            parts.Add($"{added} node(s) added");
        }
        
        if (removed > 0)
        {
            parts.Add($"{removed} node(s) removed");
        }

        if (modified > 0)
        {
            parts.Add($"{modified} node(s) modified");
        }

        var scriptChanges = changes.Where(c => c.Script != null).Take(2).ToList();
        foreach (var change in scriptChanges)
        {
            parts.Add($"{change.Name} script changed ({change.Script!.LinesAdded} added, {change.Script.LinesRemoved} removed)");
        }

        var clusterChanges = changes.Where(c => c.Type == "Cluster" || c.Type.Contains("Cluster")).Take(2).ToList();
        foreach (var change in clusterChanges)
        {
            parts.Add($"cluster '{change.Name}' internals changed");
        }

        return parts.Any() ? string.Join("; ", parts) + "." : "No changes.";
    }

    private static List<DiagnosticGroup> BuildDiagnosticsV2(List<DiffDiagnostic> diagnostics)
    {
        return diagnostics
            .GroupBy(d => d.Code)
            .Select(g => new DiagnosticGroup
            {
                Code = g.Key,
                Count = g.Count(),
                Graph = "both"
            })
            .OrderBy(d => d.Code)
            .ToList();
    }

    private static string? ComputeFileHash(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static string GetRiskLevel(int score) => score switch
    {
        >= 80 => "critical",
        >= 60 => "high",
        >= 30 => "medium",
        _ => "low"
    };

    private static string InferLanguage(string propertyKey, string nodeType)
    {
        if (propertyKey.Contains("Python", StringComparison.OrdinalIgnoreCase) || nodeType.Contains("Python"))
            return "python";
        if (propertyKey.Contains("CSharp", StringComparison.OrdinalIgnoreCase) || propertyKey.Contains("C#") || nodeType.Contains("C#"))
            return "csharp";
        if (propertyKey.Contains("VB", StringComparison.OrdinalIgnoreCase))
            return "vb";
        if (propertyKey.Contains("Expression", StringComparison.OrdinalIgnoreCase))
            return "expression";
        return "unknown";
    }

    private static bool IsScriptProperty(string key) =>
        key.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Code", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("Source", StringComparison.OrdinalIgnoreCase);

    private static bool IsOpaqueProperty(string key, string oldValue, string newValue) =>
        key.Equals("ClusterHash", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("ON_Data", StringComparison.OrdinalIgnoreCase) ||
        (oldValue.Length > 200 && oldValue.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '='));

    private static bool ShouldSkipProperty(string key) =>
        key.Equals("GUID", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Hidden", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Optional", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("SourceCount", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("NickName", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("RefID", StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions AgentJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}

internal sealed class AgentDiffReportV2
{
    public string Schema { get; set; } = string.Empty;
    public DateTimeOffset? GeneratedAt { get; set; }
    public InputFiles Inputs { get; set; } = new();
    public SummaryInfo Summary { get; set; } = new();
    public List<ChangeItem> Changes { get; set; } = new();
    public List<WireChange> Wires { get; set; } = new();
    public CosmeticChanges Cosmetic { get; set; } = new();
    public List<DiagnosticGroup> Diagnostics { get; set; } = new();
    public TruncationInfo Truncation { get; set; } = new();
}

internal sealed class InputFiles
{
    public string Old { get; set; } = string.Empty;
    public string New { get; set; } = string.Empty;
    public string? OldSha256 { get; set; }
    public string? NewSha256 { get; set; }
}

internal sealed class SummaryInfo
{
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Modified { get; set; }
    public int WiresAdded { get; set; }
    public int WiresRemoved { get; set; }
    public string MaxRisk { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
}

internal sealed class ChangeItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string[] Path { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = string.Empty;
    public RiskInfo Risk { get; set; } = new();
    public NameChange? Rename { get; set; }
    public List<ParamChange>? Params { get; set; }
    public ScriptChange? Script { get; set; }
    public ImpactInfo Impact { get; set; } = new();
}

internal sealed class NameChange
{
    public string? OldName { get; set; }
    public string? NewName { get; set; }
    public string? OldNickname { get; set; }
    public string? NewNickname { get; set; }
}

internal sealed class RiskInfo
{
    public int Score { get; set; }
    public string Level { get; set; } = string.Empty;
    public List<string> Reasons { get; set; } = new();
}

internal sealed class ParamChange
{
    public string Key { get; set; } = string.Empty;
    public string Old { get; set; } = string.Empty;
    public string New { get; set; } = string.Empty;
}

internal sealed class ScriptChange
{
    public string Language { get; set; } = string.Empty;
    public int LinesAdded { get; set; }
    public int LinesRemoved { get; set; }
    public string UnifiedDiff { get; set; } = string.Empty;
    public bool? Truncated { get; set; }
    public string? LineEndingChange { get; set; }
}

internal sealed class ImpactInfo
{
    public List<ImpactNode> Upstream { get; set; } = new();
    public List<ImpactNode> Downstream { get; set; } = new();
    public int DownstreamCount { get; set; }
}

internal sealed class ImpactNode
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string? Status { get; set; }
}

internal sealed class CosmeticChanges
{
    public int Moved { get; set; }
}

internal sealed class DiagnosticGroup
{
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
    public string Graph { get; set; } = string.Empty;
}

internal sealed class TruncationInfo
{
    public int Listed { get; set; }
    public int Total { get; set; }
    public int OmittedLowRisk { get; set; }
}

internal sealed class WireChange
{
    public string Status { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Wire { get; set; } = string.Empty;
}
