using System.Xml.Linq;
using System.Xml;
using System.Security.Cryptography;
using HopTracer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text;
using System.Reflection;

namespace HopTracer.Core.Services;

/// <summary>
/// Parses GHX (XML) files into Graph structures.
/// </summary>
public class GhxParser : IGhxParser
{
    private readonly ILogger<GhxParser> _logger;
    private const int MaxClusterPreviewDepth = 3;
    // Align cluster preview caps with viewer large-model thresholds to reduce dropped internals.
    private const int MaxClusterPreviewNodes = 4000;
    private static readonly (string CanonicalKey, string[] Aliases)[] InputBooleanOptionAliases =
    {
        ("Optional", new[] { "Optional" }),
        ("Flatten", new[] { "Flatten", "Flattened" }),
        ("Graft", new[] { "Graft", "Grafted" }),
        ("Simplify", new[] { "Simplify", "Simplified" }),
        ("Reverse", new[] { "Reverse", "Reversed" })
    };
    private static readonly (string CanonicalKey, string[] Aliases)[] InputValueOptionAliases =
    {
        ("DataMapping", new[] { "DataMapping" }),
        ("AtLeast", new[] { "AtLeast" }),
        ("AtMost", new[] { "AtMost" })
    };

    public GhxParser(ILogger<GhxParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a GHX file from the specified path.
    /// </summary>
    public Graph Parse(string path)
    {
        if (!File.Exists(path))
        {
            _logger.LogError("File not found: {Path}", path);
            throw new FileNotFoundException($"File not found: {path}");
        }

        XDocument doc;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, settings);
            doc = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse XML {Path}", path);
            throw new InvalidOperationException($"Failed to parse XML file: {ex.Message}", ex);
        }

        var diagnostics = new ParseDiagnostics();
        var graph = ParseDocument(doc, includeClusterPreview: true, clusterDepth: 0, diagnostics: diagnostics);
        graph.Metadata.UnresolvedEdgeReferences = diagnostics.UnresolvedEdgeReferences;
        graph.Metadata.ClusterPreviewParsed = diagnostics.ClusterPreviewParsed;
        graph.Metadata.ClusterPreviewFailed = diagnostics.ClusterPreviewFailed;
        graph.Metadata.ClusterPreviewDepthLimitHits = diagnostics.ClusterPreviewDepthLimitHits;
        graph.Metadata.Diagnostics = diagnostics.Messages;
        _logger.LogInformation("Parsed graph with {NodeCount} nodes and {EdgeCount} edges from {Path}", graph.Nodes.Count, graph.Edges.Count, path);
        return graph;
    }

    private Graph ParseDocument(XDocument doc, bool includeClusterPreview, int clusterDepth, ParseDiagnostics diagnostics)
    {
        var nodes = new Dictionary<string, Node>();
        var edges = new List<Edge>();
        var outputMap = new Dictionary<string, (string NodeId, string PortId)>(StringComparer.OrdinalIgnoreCase); // Output lookup id -> (NodeID, canonical PortID)

        // Find all objects (chunks with name='Object')
        var objectChunks = doc.Descendants("chunk")
            .Where(c => c.Attribute("name")?.Value == "Object")
            .ToList();

        // PASS 1: Create Nodes and Map Outputs
        foreach (var chunk in objectChunks)
        {
            var node = ParseObjectNode(chunk, includeClusterPreview, clusterDepth, diagnostics);
            if (node != null)
            {
                nodes[node.Id] = node;

                // Parse Outputs
                var outputs = ParseOutputParams(chunk, node.Id);
                foreach (var output in outputs)
                {
                    node.Outputs.Add(output.Port);
                    foreach (var lookupId in output.LookupIds)
                    {
                        outputMap[lookupId] = (node.Id, output.Port.Id);
                    }
                }
            }
        }

        // PASS 2: Parse Inputs and Collect Edges
        foreach (var chunk in objectChunks)
        {
            var guid = GetObjectInstanceGuid(chunk);
            if (string.IsNullOrEmpty(guid) || !nodes.TryGetValue(guid, out var node)) continue;

            var inputs = ParseInputParams(chunk, node.Id);

            foreach (var inputPort in inputs)
            {
                node.Inputs.Add(inputPort.Port);

                // For each source connected to this input
                foreach (var sourceGuid in inputPort.Sources)
                {
                    // PASS 3: Resolve Edges
                    if (outputMap.TryGetValue(sourceGuid, out var sourceInfo))
                    {
                        edges.Add(new Edge
                        {
                            Source = sourceInfo.NodeId,
                            SourcePort = sourceInfo.PortId,
                            Target = node.Id,
                            TargetPort = inputPort.Port.Id,
                            Status = "same"
                        });
                    }
                    else
                    {
                        diagnostics.UnresolvedEdgeReferences++;
                        if (diagnostics.Messages.Count < 25)
                        {
                            diagnostics.Messages.Add($"Unresolved edge source '{sourceGuid}' for node '{node.Nickname ?? node.Name}'.");
                        }
                    }
                }
            }
        }

        return new Graph { Nodes = nodes, Edges = edges, Metadata = ParseMetadata(doc) };
    }

    private Node? ParseObjectNode(XElement chunk, bool includeClusterPreview, int clusterDepth, ParseDiagnostics diagnostics)
    {
        var container = GetContainerChunk(chunk);
        var guid = GetObjectInstanceGuid(chunk);
        if (string.IsNullOrEmpty(guid)) return null;

        var name = (container != null ? GetDirectValue(container, "Name") : null) ?? GetValue(chunk, "Name") ?? "Unknown";
        var nickname = (container != null ? GetDirectValue(container, "NickName") : null) ?? GetValue(chunk, "NickName") ?? "";

        // Position (Pivot)
        double x = 0.0, y = 0.0;
        double w = 0.0, h = 0.0;
        var pivot = chunk.Descendants("item")
            .FirstOrDefault(i => i.Attribute("name")?.Value == "Pivot");
        
        if (pivot != null)
        {
            var xElem = pivot.Element("X");
            var yElem = pivot.Element("Y");
            
            if (xElem != null && double.TryParse(xElem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var xVal))
                x = xVal;
            if (yElem != null && double.TryParse(yElem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var yVal))
                y = yVal;
        }

        // Bounds (preferred for visual size and top-left positioning)
        var bounds = chunk.Descendants("item")
            .FirstOrDefault(i => i.Attribute("name")?.Value == "Bounds");
        if (bounds != null)
        {
            var bx = bounds.Element("X");
            var by = bounds.Element("Y");
            var bw = bounds.Element("W");
            var bh = bounds.Element("H");

            if (bx != null && double.TryParse(bx.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var xVal))
                x = xVal;
            if (by != null && double.TryParse(by.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var yVal))
                y = yVal;
            if (bw != null && double.TryParse(bw.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wVal))
                w = wVal;
            if (bh != null && double.TryParse(bh.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hVal))
                h = hVal;
        }

        var node = new Node
        {
            Id = guid,
            Name = name,
            Nickname = nickname,
            X = x,
            Y = y,
            W = w,
            H = h
        };

        // Capture properties
        var valueHints = ExtractValues(chunk, includeClusterPreview, clusterDepth, diagnostics);
        foreach (var kvp in valueHints)
        {
            node.Properties[kvp.Key] = kvp.Value;
        }

        return node;
    }

    private List<ParsedOutputPort> ParseOutputParams(XElement objectChunk, string nodeId)
    {
        var paramsList = new List<ParsedOutputPort>();
        var container = GetContainerChunk(objectChunk);
        if (container == null) return paramsList;

        // Locate ParameterData (schema v1)
        var paramData = GetDirectChunks(container)
            .FirstOrDefault(c => IsChunkNamed(c, "ParameterData"));

        if (paramData != null)
        {
            // Authoritative Output IDs (when present)
            var outputIds = GetDirectValues(paramData, "OutputId")
                .ToList();

            // OutputParam chunks under ParameterData
            var outputChunks = GetDirectChunks(paramData)
                .Where(c => IsChunkNamed(c, "OutputParam") || IsChunkNamed(c, "param_output"))
                .ToList();

            // We iterate based on the count of chunks or IDs (usually they match)
            int count = Math.Max(outputIds.Count, outputChunks.Count);

            for (int i = 0; i < count; i++)
            {
                var outChunk = i < outputChunks.Count ? outputChunks[i] : null;

                // Prefer OutputId, fallback to chunk InstanceGuid
                var outputId = i < outputIds.Count ? outputIds[i] : null;
                var chunkGuid = outChunk != null ? (GetDirectValue(outChunk, "InstanceGuid") ?? GetValue(outChunk, "InstanceGuid")) : null;
                var id = outputId ?? chunkGuid;

                if (string.IsNullOrEmpty(id)) continue;

                var name = outChunk != null ? (GetDirectValue(outChunk, "Name") ?? GetValue(outChunk, "Name") ?? "Result") : "Result";
                var nick = outChunk != null ? (GetDirectValue(outChunk, "NickName") ?? GetValue(outChunk, "NickName") ?? "") : "";
                paramsList.Add(BuildParsedOutputPort(id, name, nick, outputId, chunkGuid));
            }
        }

        // Legacy schema: direct param_output chunks under Container
        var legacyOutputChunks = GetDirectChunks(container)
            .Where(c => IsChunkNamed(c, "param_output") || IsChunkNamed(c, "OutputParam"))
            .ToList();

        foreach (var outChunk in legacyOutputChunks)
        {
            var chunkGuid = GetDirectValue(outChunk, "InstanceGuid") ?? GetValue(outChunk, "InstanceGuid");
            if (string.IsNullOrEmpty(chunkGuid)) continue;

            var name = GetDirectValue(outChunk, "Name") ?? GetValue(outChunk, "Name") ?? "Result";
            var nick = GetDirectValue(outChunk, "NickName") ?? GetValue(outChunk, "NickName") ?? "";
            paramsList.Add(BuildParsedOutputPort(chunkGuid, name, nick, chunkGuid));
        }

        return paramsList;
    }

    private List<(Port Port, List<string> Sources)> ParseInputParams(XElement objectChunk, string nodeId)
    {
        var results = new List<(Port, List<string>)>();
        var container = GetContainerChunk(objectChunk);
        if (container == null) return results;

        var paramData = GetDirectChunks(container)
            .FirstOrDefault(c => IsChunkNamed(c, "ParameterData"));

        // Schema v1: ParameterData with InputParam chunks
        if (paramData != null)
        {
            var inputIds = GetDirectValues(paramData, "InputId")
                .ToList();

            var inputChunks = GetDirectChunks(paramData)
                .Where(c => IsChunkNamed(c, "InputParam") || IsChunkNamed(c, "param_input"))
                .ToList();

            int count = Math.Max(inputIds.Count, inputChunks.Count);

            for (int i = 0; i < count; i++)
            {
                var inChunk = i < inputChunks.Count ? inputChunks[i] : null;
                var options = inChunk != null
                    ? ParseInputOptions(inChunk)
                    : new Dictionary<string, string>(StringComparer.Ordinal);

                var id = (i < inputIds.Count ? inputIds[i] : null)
                         ?? (inChunk != null ? (GetDirectValue(inChunk, "InstanceGuid") ?? GetValue(inChunk, "InstanceGuid")) : null);

                if (string.IsNullOrEmpty(id)) continue;

                var name = inChunk != null ? (GetDirectValue(inChunk, "Name") ?? GetValue(inChunk, "Name") ?? "Input") : "Input";
                var nick = inChunk != null ? (GetDirectValue(inChunk, "NickName") ?? GetValue(inChunk, "NickName") ?? "") : "";

                var sources = inChunk != null ? GetAllValues(inChunk, "Source").ToList() : new List<string>();

                results.Add((new Port
                {
                    Id = id,
                    Name = name,
                    Nickname = nick,
                    Kind = "input",
                    Status = "same",
                    Options = options,
                    WireDisplay = ParseWireDisplayMode(inChunk)
                }, sources));
            }
        }

        // Legacy schema: direct param_input chunks under Container
        var legacyInputChunks = GetDirectChunks(container)
            .Where(c => IsChunkNamed(c, "param_input") || IsChunkNamed(c, "InputParam"))
            .ToList();

        foreach (var inChunk in legacyInputChunks)
        {
            var options = ParseInputOptions(inChunk);
            var id = GetDirectValue(inChunk, "InstanceGuid")
                     ?? GetValue(inChunk, "InstanceGuid")
                     ?? $"{nodeId}:input:{results.Count}";
            var name = GetDirectValue(inChunk, "Name") ?? GetValue(inChunk, "Name") ?? "Input";
            var nick = GetDirectValue(inChunk, "NickName") ?? GetValue(inChunk, "NickName") ?? "";
            var sources = GetAllValues(inChunk, "Source").ToList();

            results.Add((new Port
            {
                Id = id,
                Name = name,
                Nickname = nick,
                Kind = "input",
                Status = "same",
                Options = options,
                WireDisplay = ParseWireDisplayMode(inChunk)
            }, sources));
        }

        // Some objects store source references directly on the container itself.
        if (results.Count == 0)
        {
            var directSources = GetDirectValues(container, "Source").ToList();
            if (directSources.Count > 0)
            {
                results.Add((new Port
                {
                    Id = $"{nodeId}:input:direct",
                    Name = "Input",
                    Nickname = "",
                    Kind = "input",
                    Status = "same",
                    Options = new Dictionary<string, string>(StringComparer.Ordinal),
                    WireDisplay = 0
                }, directSources));
            }
        }

        return results;
    }

    private ParsedOutputPort BuildParsedOutputPort(string id, string name, string nickname, params string?[] aliases)
    {
        var lookupIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(id)) lookupIds.Add(id.Trim());
        foreach (var alias in aliases)
        {
            if (!string.IsNullOrWhiteSpace(alias)) lookupIds.Add(alias.Trim());
        }

        return new ParsedOutputPort
        {
            Port = new Port
            {
                Id = id.Trim(),
                Name = name,
                Nickname = nickname,
                Kind = "output",
                Status = "same",
                WireDisplay = 0
            },
            LookupIds = lookupIds.ToList()
        };
    }

    private int ParseWireDisplayMode(XElement? paramChunk)
    {
        if (paramChunk == null) return 0;

        var raw = GetDirectValue(paramChunk, "WireDisplay")
            ?? GetValue(paramChunk, "WireDisplay")
            ?? GetDirectValue(paramChunk, "WireDisplayMode")
            ?? GetValue(paramChunk, "WireDisplayMode");
        if (string.IsNullOrWhiteSpace(raw)) return 0;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return Math.Clamp(numeric, 0, 2);
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized.Contains("faint")) return 1;
        if (normalized.Contains("hidden")) return 2;
        return 0;
    }

    private Dictionary<string, string> ParseInputOptions(XElement? paramChunk)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (paramChunk == null)
        {
            return options;
        }

        foreach (var (canonicalKey, aliases) in InputBooleanOptionAliases)
        {
            var raw = GetFirstOptionValue(paramChunk, aliases);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            if (TryParseBooleanOptionValue(raw, out var enabled))
            {
                if (enabled)
                {
                    options[canonicalKey] = "true";
                }

                continue;
            }

            options[canonicalKey] = raw.Trim();
        }

        var access = ResolveInputAccess(paramChunk);
        if (!string.IsNullOrWhiteSpace(access))
        {
            options["Access"] = access;
        }

        foreach (var (canonicalKey, aliases) in InputValueOptionAliases)
        {
            var raw = GetFirstOptionValue(paramChunk, aliases);
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            options[canonicalKey] = NormalizeInputOptionValue(canonicalKey, raw);
        }

        return options;
    }

    private string? ResolveInputAccess(XElement paramChunk)
    {
        var raw = GetFirstOptionValue(paramChunk, "Access", "ParameterAccess");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            return NormalizeAccessValue(raw);
        }

        if (OptionEnabled(paramChunk, "ItemAccess"))
        {
            return "item";
        }

        if (OptionEnabled(paramChunk, "ListAccess"))
        {
            return "list";
        }

        if (OptionEnabled(paramChunk, "TreeAccess"))
        {
            return "tree";
        }

        return null;
    }

    private bool OptionEnabled(XElement paramChunk, string name)
    {
        var raw = GetDirectValue(paramChunk, name) ?? GetValue(paramChunk, name);
        return TryParseBooleanOptionValue(raw, out var enabled) && enabled;
    }

    private string? GetFirstOptionValue(XElement paramChunk, params string[] names)
    {
        foreach (var name in names)
        {
            var raw = GetDirectValue(paramChunk, name) ?? GetValue(paramChunk, name);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }
        }

        return null;
    }

    private static bool TryParseBooleanOptionValue(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "true":
            case "1":
            case "yes":
            case "on":
                value = true;
                return true;
            case "false":
            case "0":
            case "no":
            case "off":
                value = false;
                return true;
            default:
                return false;
        }
    }

    private static string NormalizeInputOptionValue(string key, string raw)
    {
        var trimmed = raw.Trim();
        if (string.Equals(key, "DataMapping", StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeDataMappingValue(trimmed);
        }

        return trimmed;
    }

    private static string NormalizeAccessValue(string raw)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        return normalized switch
        {
            "0" => "item",
            "1" => "list",
            "2" => "tree",
            "item" => "item",
            "list" => "list",
            "tree" => "tree",
            _ when normalized.Contains("item", StringComparison.Ordinal) => "item",
            _ when normalized.Contains("list", StringComparison.Ordinal) => "list",
            _ when normalized.Contains("tree", StringComparison.Ordinal) => "tree",
            _ => normalized
        };
    }

    private static string NormalizeDataMappingValue(string raw)
    {
        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized.Contains("flatten", StringComparison.Ordinal))
        {
            return "flatten";
        }

        if (normalized.Contains("graft", StringComparison.Ordinal))
        {
            return "graft";
        }

        if (normalized.Contains("simplify", StringComparison.Ordinal))
        {
            return "simplify";
        }

        if (normalized.Contains("reverse", StringComparison.Ordinal))
        {
            return "reverse";
        }

        if (normalized.Contains("none", StringComparison.Ordinal) ||
            normalized.Contains("default", StringComparison.Ordinal))
        {
            return "default";
        }

        return normalized;
    }

    private string? GetObjectInstanceGuid(XElement objectChunk)
    {
        var container = GetContainerChunk(objectChunk);
        return (container != null ? GetDirectValue(container, "InstanceGuid") : null) ?? GetValue(objectChunk, "InstanceGuid");
    }

    private static XElement? GetContainerChunk(XElement objectChunk)
    {
        return GetDirectChunks(objectChunk).FirstOrDefault(c => IsChunkNamed(c, "Container"))
            ?? objectChunk.Descendants("chunk").FirstOrDefault(c => IsChunkNamed(c, "Container"));
    }

    private static IEnumerable<XElement> GetDirectChunks(XElement parent)
    {
        return parent.Element("chunks")?.Elements("chunk") ?? Enumerable.Empty<XElement>();
    }

    private static bool IsChunkNamed(XElement chunk, string name)
    {
        return string.Equals(chunk.Attribute("name")?.Value, name, StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetDirectValue(XElement elem, string name)
    {
        var item = elem.Element("items")?
            .Elements("item")
            .FirstOrDefault(i => string.Equals(i.Attribute("name")?.Value, name, StringComparison.OrdinalIgnoreCase));

        return item?.Value?.Trim();
    }

    private static IEnumerable<string> GetDirectValues(XElement elem, string name)
    {
        return (elem.Element("items")?
            .Elements("item")
            .Where(i => string.Equals(i.Attribute("name")?.Value, name, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Value?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>()) ?? Enumerable.Empty<string>();
    }

    private static IEnumerable<string> GetAllValues(XElement elem, string name)
    {
        return elem.Descendants("item")
            .Where(i => string.Equals(i.Attribute("name")?.Value, name, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Value?.Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Cast<string>();
    }

    private GraphMetadata ParseMetadata(XDocument doc)
    {
        var meta = new GraphMetadata();
        meta.ArchiveVersion = GetVersionValue(doc.Root, "ArchiveVersion", compactMinorRevision: false) ?? "";
        
        var defChunk = doc.Descendants("chunk").FirstOrDefault(c => c.Attribute("name")?.Value == "Definition");
        if (defChunk == null) return meta;

        meta.FileVersion = GetVersionValue(defChunk, "plugin_version", compactMinorRevision: true) ?? "";

        var propsChunk = defChunk.Descendants("chunk").FirstOrDefault(c => c.Attribute("name")?.Value == "DefinitionProperties");
        if (propsChunk != null)
        {
            meta.Name = GetValue(propsChunk, "Name") ?? "";
            meta.Description = GetValue(propsChunk, "Description") ?? "";
            
            var dateStr = GetValue(propsChunk, "Date");
            if (long.TryParse(dateStr, out var ticks))
            {
                try { meta.Date = new DateTime(ticks); } catch { }
            }
        }
        
        return meta;
    }

    private static string? GetVersionValue(XElement? parent, string itemName, bool compactMinorRevision)
    {
        if (parent == null)
        {
            return null;
        }

        var item = parent.Element("items")?
            .Elements("item")
            .FirstOrDefault(i => string.Equals(i.Attribute("name")?.Value, itemName, StringComparison.OrdinalIgnoreCase));

        return FormatVersionValue(item, compactMinorRevision);
    }

    private static string? FormatVersionValue(XElement? versionItem, bool compactMinorRevision)
    {
        if (versionItem == null)
        {
            return null;
        }

        if (!int.TryParse(versionItem.Element("Major")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major))
        {
            return null;
        }

        _ = int.TryParse(versionItem.Element("Minor")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor);
        _ = int.TryParse(versionItem.Element("Revision")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var revision);

        return compactMinorRevision
            ? $"{major}.{(minor * 100) + revision:000}"
            : $"{major}.{minor}.{revision}";
    }

    private string? GetValue(XElement elem, string name)
    {
        var item = elem.Descendants("item")
            .FirstOrDefault(i => string.Equals(i.Attribute("name")?.Value, name, StringComparison.OrdinalIgnoreCase));
        
        return item?.Value?.Trim();
    }

    private Dictionary<string, string> ExtractValues(XElement chunk, bool includeClusterPreview, int clusterDepth, ParseDiagnostics diagnostics)
    {
        var result = new Dictionary<string, string>();

        // 1. Direct value items 
        var directValues = new[] { 
            "Value", "Number", "Text", "String", "Expression", "Code", "Script",
            "ScriptSource", "SourceCode", "UsingSource", "AdditionalSource",
            "CodeInput", "CodeOutput",
            "PythonScript", "PythonCode", "CSharpCode", "VBCode", "ScriptBody",
            "UserText", "PanelContent",
            "Minimum", "Maximum", "Count", "Factor", "Length", "Width", "Height",
            "Radius", "Diameter", "Angle", "Distance", "Tolerance"
        };
        
        foreach (var name in directValues)
        {
            var val = GetValue(chunk, name);
            if (!string.IsNullOrEmpty(val))
            {
                result[name] = val;
            }
        }

        // 2. Script source code 
        var scriptSource = chunk.Descendants("item")
            .FirstOrDefault(i => i.Attribute("name")?.Value == "ScriptSource");
        if (scriptSource != null)
        {
            var code = scriptSource.Value?.Trim();
            if (!string.IsNullOrEmpty(code)) result["ScriptSource"] = code;
        }

        // Script components may store code under Script/Text (often as Base64).
        var scriptChunk = chunk.Descendants("chunk")
            .FirstOrDefault(c => string.Equals(c.Attribute("name")?.Value, "Script", StringComparison.OrdinalIgnoreCase));
        if (scriptChunk != null)
        {
            var rawScriptText = GetDirectValue(scriptChunk, "Text") ?? GetValue(scriptChunk, "Text");
            var normalizedScript = NormalizeScriptText(rawScriptText);
            if (!string.IsNullOrWhiteSpace(normalizedScript))
            {
                result["ScriptSource"] = normalizedScript;
            }
        }

        // Fallback: some script components store code in top-level Text without a Script chunk.
        if (!result.ContainsKey("ScriptSource") &&
            result.TryGetValue("Text", out var rawText) &&
            LooksLikeScriptContainer(chunk, result))
        {
            var normalizedScript = NormalizeScriptText(rawText);
            if (!string.IsNullOrWhiteSpace(normalizedScript))
            {
                result["ScriptSource"] = normalizedScript;
            }
        }

        // 3. Panel contents (FIX: Ensure UserText is captured correctly)
        // Panels usually store 'UserText' in 'PanelProperties'
        var panelProps = chunk.Descendants("chunk")
            .FirstOrDefault(c => string.Equals(c.Attribute("name")?.Value, "PanelProperties", StringComparison.OrdinalIgnoreCase));
        string? userText = null;
        if (panelProps != null)
        {
            userText = GetDirectValue(panelProps, "UserText") ?? GetValue(panelProps, "UserText");
        }

        // Common GHX panel shape stores UserText on the Container items, not PanelProperties.
        if (string.IsNullOrWhiteSpace(userText))
        {
            var panelContainer = GetContainerChunk(chunk);
            userText = (panelContainer != null ? (GetDirectValue(panelContainer, "UserText") ?? GetValue(panelContainer, "UserText")) : null)
                ?? GetDirectValue(chunk, "UserText")
                ?? GetValue(chunk, "UserText");
        }

        if (!string.IsNullOrWhiteSpace(userText))
        {
            result["PanelContent"] = userText;
            result["UserText"] = userText;
        }

        // 4. Persistent data
        var persistentData = chunk.Descendants("chunk")
            .FirstOrDefault(c => c.Attribute("name")?.Value == "PersistentData");
        if (persistentData != null)
        {
            var dataItems = persistentData.Descendants("item")
                .Where(i => i.Attribute("name")?.Value == "Data")
                .Select(i => i.Value?.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
            
            if (dataItems.Any()) result["PersistentData"] = string.Join("\n", dataItems);
        }

        // 5. Container/group properties
        var containerType = GetValue(chunk, "ContainerType");
        if (!string.IsNullOrEmpty(containerType)) result["ContainerType"] = containerType;

        var enabled = GetValue(chunk, "Enabled");
        if (!string.IsNullOrEmpty(enabled)) result["Enabled"] = enabled;

        var filePath = GetValue(chunk, "FilePath") ?? GetValue(chunk, "Path");
        if (!string.IsNullOrEmpty(filePath)) result["FilePath"] = filePath;

        var pattern = GetValue(chunk, "Pattern");
        if (!string.IsNullOrEmpty(pattern)) result["Pattern"] = pattern;

        var container = GetContainerChunk(chunk);
        if (container != null && IsGroupContainer(chunk, container))
        {
            result["IsGroup"] = "true";

            var memberIds = GetDirectValues(container, "ID")
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (memberIds.Count > 0)
            {
                result["GroupMemberIds"] = JsonSerializer.Serialize(memberIds);
                result["GroupMemberCount"] = memberIds.Count.ToString(CultureInfo.InvariantCulture);
            }

            var declaredMemberCount = GetDirectValue(container, "ID_Count");
            if (!string.IsNullOrWhiteSpace(declaredMemberCount))
            {
                result["GroupMemberCount"] = declaredMemberCount;
            }

            var rawGroupColor = GetDirectValue(container, "Colour") ?? GetDirectValue(container, "Color");
            if (!string.IsNullOrWhiteSpace(rawGroupColor))
            {
                result["GroupColor"] = NormalizeGhColor(rawGroupColor);
            }
        }

        // 6. Cluster (embedded sub-definition) detection
        var clusterDoc = chunk.Descendants("item")
            .FirstOrDefault(i => i.Attribute("name")?.Value == "ClusterDocument");
        if (clusterDoc != null)
        {
            result["IsCluster"] = "true";
            
            // The cluster content is Base64-encoded binary inside a <stream> element
            var streamElem = clusterDoc.Element("stream");
            var base64Content = streamElem?.Value?.Trim() ?? clusterDoc.Value?.Trim();
            
            if (!string.IsNullOrEmpty(base64Content))
            {
                try
                {
                    var bytes = Convert.FromBase64String(base64Content);
                    var hash = SHA256.HashData(bytes);
                    result["ClusterHash"] = Convert.ToHexString(hash);
                    result["ClusterSize"] = bytes.Length.ToString();

                    if (includeClusterPreview)
                    {
                        AttachClusterPreview(result, bytes, clusterDepth + 1, diagnostics);
                    }
                    else
                    {
                        result["ClusterPreviewStatus"] = "skipped";
                        result["ClusterPreviewMessage"] = "Cluster preview skipped at current recursion depth.";
                    }
                }
                catch (FormatException)
                {
                    _logger.LogWarning("Failed to decode ClusterDocument Base64 content");
                    result["ClusterHash"] = "DECODE_ERROR";
                    result["ClusterPreviewStatus"] = "unavailable";
                    result["ClusterPreviewMessage"] = "Cluster payload is not valid Base64.";
                    diagnostics.ClusterPreviewFailed++;
                }
            }
        }

        // 7. Generic fallback
        var allItems = chunk.Descendants("item")
            .Where(i => {
                var name = i.Attribute("name")?.Value;
                var val = i.Value?.Trim();
                return !string.IsNullOrEmpty(name) && 
                       !string.IsNullOrEmpty(val) &&
                       !result.ContainsKey(name) && 
                       name != "InstanceGuid" && 
                       name != "Name" &&
                       name != "NickName" &&
                       name != "Description" &&
                       name != "Pivot" &&
                       name != "Source" &&
                       name != "UserText" && // Handled
                       name != "ClusterDocument"; // Binary data, handled above
            });

        foreach (var item in allItems)
        {
            var name = item.Attribute("name")?.Value;
            var val = item.Value?.Trim();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(val))
            {
                if (name == "Bounds") continue; // Skip bounds, too noisy
                if (val.Length > 1000 && !IsScriptLikeKey(name)) val = val.Substring(0, 1000) + "...";
                result[name] = val;
            }
        }

        return result;
    }

    private void AttachClusterPreview(Dictionary<string, string> result, byte[] bytes, int clusterDepth, ParseDiagnostics diagnostics)
    {
        try
        {
            if (clusterDepth > MaxClusterPreviewDepth)
            {
                diagnostics.ClusterPreviewDepthLimitHits++;
                result["ClusterPreviewStatus"] = "depth_limited";
                result["ClusterPreviewMessage"] = $"Cluster preview depth limit ({MaxClusterPreviewDepth}) reached.";
                return;
            }

            var clusterXml = TryDeserializeClusterXmlWithGhIo(bytes);
            if (string.IsNullOrWhiteSpace(clusterXml))
            {
                clusterXml = TryDecodeClusterXmlText(bytes) ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(clusterXml))
            {
                diagnostics.ClusterPreviewFailed++;
                result["ClusterPreviewStatus"] = "unavailable";
                result["ClusterPreviewMessage"] = "Cluster archive did not produce XML content.";
                return;
            }

            var clusterDoc = XDocument.Parse(clusterXml, LoadOptions.PreserveWhitespace);
            var clusterGraph = ParseDocument(
                clusterDoc,
                includeClusterPreview: clusterDepth < MaxClusterPreviewDepth,
                clusterDepth: clusterDepth,
                diagnostics: diagnostics);

            var totalNodes = clusterGraph.Nodes.Count;
            var totalEdges = clusterGraph.Edges.Count;

            var payload = new ClusterPreviewPayload
            {
                Nodes = clusterGraph.Nodes.Values
                    .Take(MaxClusterPreviewNodes)
                    .Select(n => new ClusterPreviewNode
                    {
                        Id = n.Id,
                        Name = n.Name,
                        Nickname = n.Nickname,
                        X = n.X,
                        Y = n.Y,
                        W = n.W,
                        H = n.H,
                        Properties = BuildClusterPreviewProperties(n.Properties),
                        Inputs = BuildClusterPreviewPorts(n.Inputs),
                        Outputs = BuildClusterPreviewPorts(n.Outputs),
                        IsCluster = IsClusterNode(n.Properties),
                        ClusterHash = GetPropertyValue(n.Properties, "ClusterHash"),
                        NestedClusterCount = CountNestedClusters(n.Properties),
                        NestedClusterChanges = CountNestedClusterChanges(n.Properties)
                    })
                    .ToList(),
                Edges = clusterGraph.Edges
                    .Select(e => new ClusterPreviewEdge
                    {
                        Source = e.Source,
                        SourcePort = e.SourcePort,
                        Target = e.Target,
                        TargetPort = e.TargetPort
                    })
                    .ToList(),
                Truncated = totalNodes > MaxClusterPreviewNodes,
                Depth = clusterDepth
            };

            result["ClusterPreviewGraph"] = JsonSerializer.Serialize(payload);
            result["ClusterPreviewStatus"] = "parsed";
            result["ClusterPreviewDepth"] = clusterDepth.ToString(CultureInfo.InvariantCulture);
            result["ClusterPreviewMessage"] = payload.Truncated
                ? $"Preview truncated to {payload.Nodes.Count} nodes / {payload.Edges.Count} edges."
                : $"{totalNodes} nodes, {totalEdges} edges.";
            diagnostics.ClusterPreviewParsed++;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Unable to parse embedded cluster preview data");
            diagnostics.ClusterPreviewFailed++;
            result["ClusterPreviewStatus"] = "unavailable";
            result["ClusterPreviewMessage"] = "Cluster internals could not be decoded in this environment.";
        }
    }

    private static string TryDeserializeClusterXmlWithGhIo(byte[] bytes)
    {
        try
        {
            // Optional dependency: GH_IO may not be present in CI or dev environments.
            var ghArchiveType = ResolveGhArchiveType();
            if (ghArchiveType == null)
            {
                return string.Empty;
            }

            var archive = Activator.CreateInstance(ghArchiveType);
            if (archive == null)
            {
                return string.Empty;
            }

            var deserializeBinary = ghArchiveType.GetMethod("Deserialize_Binary", new[] { typeof(byte[]) });
            var serializeXml = ghArchiveType.GetMethod("Serialize_Xml", Type.EmptyTypes);
            if (deserializeBinary == null || serializeXml == null)
            {
                return string.Empty;
            }

            var deserialized = deserializeBinary.Invoke(archive, new object[] { bytes }) is bool ok && ok;
            if (!deserialized)
            {
                return string.Empty;
            }

            return serializeXml.Invoke(archive, null) as string ?? string.Empty;
        }
        catch
        {
            return string.Empty;
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
                // Optional dependency probing should never fail graph parsing.
            }
        }

        return null;
    }

    private static bool IsClusterNode(Dictionary<string, string> props)
    {
        var val = GetPropertyValue(props, "IsCluster");
        return string.Equals(val, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> BuildClusterPreviewProperties(Dictionary<string, string> props)
    {
        if (props == null || props.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // Include full node details for cluster preview inspector, but avoid recursive/noisy preview metadata.
        var blocked = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ClusterPreviewGraph",
            "ClusterPreviewStatus",
            "ClusterPreviewMessage",
            "ClusterPreviewDepth"
        };

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in props)
        {
            if (!blocked.Contains(kv.Key))
            {
                result[kv.Key] = kv.Value;
            }
        }

        return result;
    }

    private static List<ClusterPreviewPort> BuildClusterPreviewPorts(List<Port> ports)
    {
        if (ports == null || ports.Count == 0)
        {
            return new List<ClusterPreviewPort>();
        }

        return ports.Select(p => new ClusterPreviewPort
        {
            Id = p.Id,
            Name = p.Name,
            Nickname = p.Nickname,
            Kind = p.Kind,
            Type = p.Type,
            Value = p.Value,
            Options = new Dictionary<string, string>(p.Options, StringComparer.Ordinal),
            WireDisplay = p.WireDisplay
        }).ToList();
    }

    private static string? GetPropertyValue(Dictionary<string, string> props, string key)
    {
        if (props.TryGetValue(key, out var value))
        {
            return value;
        }

        var alt = char.ToLowerInvariant(key[0]) + key[1..];
        return props.TryGetValue(alt, out var altValue) ? altValue : null;
    }

    private static bool IsScriptLikeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var lower = key.ToLowerInvariant();
        // Rhino 7 C# script: UsingSource / AdditionalSource do not contain "script" or "code" in the name.
        if (lower is "usingsource" or "additionalsource")
        {
            return true;
        }

        return lower.Contains("script") ||
               lower.Contains("code") ||
               lower.Contains("python") ||
               lower.Contains("csharp") ||
               lower.Contains("expression");
    }

    private static bool LooksLikeScriptContainer(XElement chunk, Dictionary<string, string> values)
    {
        if (values.Keys.Any(IsScriptLikeKey))
        {
            return true;
        }

        var title = GetDirectValue(chunk, "Title") ?? string.Empty;
        var name = GetDirectValue(chunk, "Name") ?? string.Empty;
        var nick = GetDirectValue(chunk, "NickName") ?? string.Empty;
        var combined = $"{title} {name} {nick}".ToLowerInvariant();

        return combined.Contains("script", StringComparison.Ordinal) ||
               combined.Contains("c#", StringComparison.Ordinal) ||
               combined.Contains("csharp", StringComparison.Ordinal) ||
               combined.Contains("python", StringComparison.Ordinal) ||
               combined.Contains("ghpython", StringComparison.Ordinal) ||
               combined.Contains("vb", StringComparison.Ordinal);
    }

    private static string? NormalizeScriptText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        if (TryDecodeBase64ToUtf8(trimmed, out var decoded) && IsMostlyTextual(decoded))
        {
            return decoded;
        }

        if (IsMostlyTextual(trimmed))
        {
            return trimmed;
        }

        return null;
    }

    private static bool TryDecodeBase64ToUtf8(string text, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var normalized = text.Replace("\r", string.Empty, StringComparison.Ordinal)
                                 .Replace("\n", string.Empty, StringComparison.Ordinal)
                                 .Trim();
            var bytes = Convert.FromBase64String(normalized);
            decoded = Encoding.UTF8.GetString(bytes).Trim('\0', '\uFEFF');
            return !string.IsNullOrWhiteSpace(decoded);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsMostlyTextual(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var total = 0;
        var printable = 0;
        foreach (var ch in text)
        {
            total++;
            if (ch is '\r' or '\n' or '\t')
            {
                printable++;
                continue;
            }

            if (!char.IsControl(ch))
            {
                printable++;
            }
        }

        if (total == 0)
        {
            return false;
        }

        return (printable / (double)total) >= 0.9;
    }

    private static string? TryDecodeClusterXmlText(byte[] bytes)
    {
        static string TrimBomAndWhitespace(string s) => s.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');

        try
        {
            var asUtf8 = TrimBomAndWhitespace(Encoding.UTF8.GetString(bytes));
            if (asUtf8.StartsWith("<", StringComparison.Ordinal))
            {
                return asUtf8;
            }

            var looksBase64 = asUtf8.Length > 16 && asUtf8.All(c => char.IsLetterOrDigit(c) || c is '+' or '/' or '=' or '\r' or '\n');
            if (looksBase64)
            {
                var nestedBytes = Convert.FromBase64String(asUtf8);
                var nested = TrimBomAndWhitespace(Encoding.UTF8.GetString(nestedBytes));
                if (nested.StartsWith("<", StringComparison.Ordinal))
                {
                    return nested;
                }
            }
        }
        catch
        {
            // Best-effort fallback only.
        }

        return null;
    }

    private static int CountNestedClusters(Dictionary<string, string> props)
    {
        var raw = GetPropertyValue(props, "ClusterPreviewGraph");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("nodes", out var nodes) && !doc.RootElement.TryGetProperty("Nodes", out nodes))
            {
                return 0;
            }

            var count = 0;
            foreach (var node in nodes.EnumerateArray())
            {
                if ((node.TryGetProperty("isCluster", out var isCluster) && isCluster.GetBoolean()) ||
                    (node.TryGetProperty("IsCluster", out isCluster) && isCluster.GetBoolean()))
                {
                    count++;
                }
            }

            return count;
        }
        catch
        {
            return 0;
        }
    }

    private static int CountNestedClusterChanges(Dictionary<string, string> props)
    {
        var hash = GetPropertyValue(props, "ClusterHash");
        return string.IsNullOrWhiteSpace(hash) ? 0 : 1;
    }

    private bool IsGroupContainer(XElement objectChunk, XElement container)
    {
        var containerName = GetDirectValue(container, "Name");
        if (string.Equals(containerName, "Group", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var objectName = GetValue(objectChunk, "Name");
        if (string.Equals(objectName, "Group", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var description = GetDirectValue(container, "Description") ?? GetValue(container, "Description");
        if (!string.IsNullOrWhiteSpace(description) &&
            description.Contains("group of grasshopper objects", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string NormalizeGhColor(string rawColor)
    {
        if (string.IsNullOrWhiteSpace(rawColor))
        {
            return string.Empty;
        }

        var numbers = rawColor
            .Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? (int?)value : null)
            .Where(value => value.HasValue)
            .Select(value => Math.Clamp(value!.Value, 0, 255))
            .ToList();

        if (numbers.Count >= 4)
        {
            return $"{numbers[0]};{numbers[1]};{numbers[2]};{numbers[3]}";
        }

        return rawColor.Trim();
    }

    private sealed class ParsedOutputPort
    {
        public Port Port { get; set; } = new();
        public List<string> LookupIds { get; set; } = new();
    }

    private sealed class ClusterPreviewPayload
    {
        public List<ClusterPreviewNode> Nodes { get; set; } = new();
        public List<ClusterPreviewEdge> Edges { get; set; } = new();
        public bool Truncated { get; set; }
        public int Depth { get; set; }
    }

    private sealed class ClusterPreviewNode
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new();
        public List<ClusterPreviewPort> Inputs { get; set; } = new();
        public List<ClusterPreviewPort> Outputs { get; set; } = new();
        public bool IsCluster { get; set; }
        public string? ClusterHash { get; set; }
        public int NestedClusterCount { get; set; }
        public int NestedClusterChanges { get; set; }
    }

    private sealed class ClusterPreviewEdge
    {
        public string Source { get; set; } = string.Empty;
        public string SourcePort { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string TargetPort { get; set; } = string.Empty;
    }

    private sealed class ClusterPreviewPort
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? Value { get; set; }
        public Dictionary<string, string> Options { get; set; } = new();
        public int WireDisplay { get; set; }
    }

    private sealed class ParseDiagnostics
    {
        public int UnresolvedEdgeReferences { get; set; }
        public int ClusterPreviewParsed { get; set; }
        public int ClusterPreviewFailed { get; set; }
        public int ClusterPreviewDepthLimitHits { get; set; }
        public List<string> Messages { get; set; } = new();
    }
}
