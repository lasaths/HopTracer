using System.Xml.Linq;
using System.Xml;
using HopTracer.Core.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace HopTracer.Core.Services;

/// <summary>
/// Parses GHX (XML) files into Graph structures.
/// </summary>
public class GhxParser : IGhxParser
{
    private readonly ILogger<GhxParser> _logger;

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

        var nodes = new Dictionary<string, Node>();
        var edges = new List<Edge>();

        // Find all objects (chunks with name='Object')
        var objectChunks = doc.Descendants("chunk")
            .Where(c => c.Attribute("name")?.Value == "Object");

        foreach (var chunk in objectChunks)
        {
            var node = ParseObject(chunk);
            if (node != null)
            {
                nodes[node.Id] = node;

                // Extract sources (edges)
                var sources = ParseSources(chunk).ToList();
                EnsureInputPorts(node, sources.Count);

                var portIndex = 0;
                foreach (var srcGuid in sources)
                {
                    edges.Add(new Edge
                    {
                        Source = srcGuid,
                        SourcePort = $"{srcGuid}:out:0",
                        Target = node.Id,
                        TargetPort = $"{node.Id}:in:{portIndex}",
                        Status = "same"
                    });
                    portIndex++;
                }
            }
        }

        // Ensure outputs exist for nodes referenced as sources
        foreach (var edge in edges)
        {
            if (nodes.TryGetValue(edge.Source, out var srcNode))
            {
                if (srcNode.Outputs.All(p => p.Id != edge.SourcePort))
                {
                    srcNode.Outputs.Add(new Port
                    {
                        Id = edge.SourcePort,
                        Name = "Out",
                        Nickname = "",
                        Kind = "output",
                        Type = "",
                        Status = "same"
                    });
                }
            }
        }

        _logger.LogInformation("Parsed graph with {NodeCount} nodes and {EdgeCount} edges from {Path}", nodes.Count, edges.Count, path);
        return new Graph { Nodes = nodes, Edges = edges, Metadata = ParseMetadata(doc) };
    }

    private GraphMetadata ParseMetadata(XDocument doc)
    {
        var meta = new GraphMetadata();
        
        var defChunk = doc.Descendants("chunk").FirstOrDefault(c => c.Attribute("name")?.Value == "Definition");
        if (defChunk == null) return meta;

        var propsChunk = defChunk.Descendants("chunk").FirstOrDefault(c => c.Attribute("name")?.Value == "DefinitionProperties");
        if (propsChunk != null)
        {
            meta.Name = GetValue(propsChunk, "Name") ?? "";
            meta.Description = GetValue(propsChunk, "Description") ?? "";
            
            var dateStr = GetValue(propsChunk, "Date");
            if (long.TryParse(dateStr, out var ticks))
            {
                // GH uses Ticks
                try { meta.Date = new DateTime(ticks); } catch { }
            }
        }
        
        return meta;
    }

    /// <summary>
    /// Gets the text value of a named item within an element.
    /// </summary>
    private string? GetValue(XElement elem, string name)
    {
        var item = elem.Descendants("item")
            .FirstOrDefault(i => i.Attribute("name")?.Value == name);
        
        return item?.Value?.Trim();
    }

    /// <summary>
    /// Parses a single object chunk into a Node.
    /// </summary>
    private Node? ParseObject(XElement chunk)
    {
        var guid = GetValue(chunk, "InstanceGuid");
        if (string.IsNullOrEmpty(guid))
        {
            return null;
        }

        var name = GetValue(chunk, "Name") ?? "Unknown";
        var nickname = GetValue(chunk, "NickName") ?? "";

        // Position (Pivot)
        double x = 0.0, y = 0.0;
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

        var node = new Node
        {
            Id = guid,
            Name = name,
            Nickname = nickname,
            X = x,
            Y = y
        };

        // Capture simple property/value hints (best-effort)
        var valueHints = ExtractValues(chunk);
        foreach (var kvp in valueHints)
        {
            node.Properties[kvp.Key] = kvp.Value;
        }

        return node;
    }

    /// <summary>
    /// Extracts all source GUIDs (upstream connections) from an object chunk.
    /// </summary>
    private List<string> ParseSources(XElement chunk)
    {
        var sources = new List<string>();
        
        // Find all items named "Source"
        var sourceItems = chunk.Descendants("item")
            .Where(i => i.Attribute("name")?.Value == "Source");

        foreach (var item in sourceItems)
        {
            var value = item.Value?.Trim();
            if (!string.IsNullOrEmpty(value))
            {
                sources.Add(value);
            }
        }

        return sources;
    }

    private void EnsureInputPorts(Node node, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var portId = $"{node.Id}:in:{i}";
            if (node.Inputs.All(p => p.Id != portId))
            {
                node.Inputs.Add(new Port
                {
                    Id = portId,
                    Name = $"In {i + 1}",
                    Nickname = "",
                    Kind = "input",
                    Type = "",
                    Status = "same"
                });
            }
        }
    }


    /// <summary>
    /// Comprehensive extraction of component values, data, and code for diffing.
    /// Captures panels, sliders, number boxes, script source, persistent data, etc.
    /// </summary>
    private Dictionary<string, string> ExtractValues(XElement chunk)
    {
        var result = new Dictionary<string, string>();

        // 1. Direct value items (sliders, number boxes, text, etc.)
        var directValues = new[] { 
            "Value", "Number", "Text", "String", "Expression", "Code", "Script",
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

        // 2. Script source code (Python, C#, VB scripts)
        var scriptSource = chunk.Descendants("item")
            .FirstOrDefault(i => i.Attribute("name")?.Value == "ScriptSource");
        if (scriptSource != null)
        {
            var code = scriptSource.Value?.Trim();
            if (!string.IsNullOrEmpty(code))
            {
                result["ScriptSource"] = code;
            }
        }

        // 3. Panel contents (multi-line text display)
        var panelProps = chunk.Descendants("chunk")
            .FirstOrDefault(c => c.Attribute("name")?.Value == "PanelProperties");
        if (panelProps != null)
        {
            var userText = GetValue(panelProps, "UserText");
            if (!string.IsNullOrEmpty(userText))
            {
                result["PanelContent"] = userText;
            }
        }

        // 4. Persistent data (stored parameter values)
        var persistentData = chunk.Descendants("chunk")
            .FirstOrDefault(c => c.Attribute("name")?.Value == "PersistentData");
        if (persistentData != null)
        {
            // Extract all data items
            var dataItems = persistentData.Descendants("item")
                .Where(i => i.Attribute("name")?.Value == "Data")
                .Select(i => i.Value?.Trim())
                .Where(v => !string.IsNullOrEmpty(v))
                .ToList();
            
            if (dataItems.Any())
            {
                result["PersistentData"] = string.Join("\n", dataItems);
            }
        }

        // 5. Container/group properties
        var containerType = GetValue(chunk, "ContainerType");
        if (!string.IsNullOrEmpty(containerType))
        {
            result["ContainerType"] = containerType;
        }

        // 6. Boolean toggles, buttons
        var enabled = GetValue(chunk, "Enabled");
        if (!string.IsNullOrEmpty(enabled))
        {
            result["Enabled"] = enabled;
        }

        // 7. File/path references
        var filePath = GetValue(chunk, "FilePath") ?? GetValue(chunk, "Path");
        if (!string.IsNullOrEmpty(filePath))
        {
            result["FilePath"] = filePath;
        }

        // 8. Curve/surface parameters
        var degree = GetValue(chunk, "Degree");
        if (!string.IsNullOrEmpty(degree))
        {
            result["Degree"] = degree;
        }

        // 9. List/tree operations
        var pattern = GetValue(chunk, "Pattern");
        if (!string.IsNullOrEmpty(pattern))
        {
            result["Pattern"] = pattern;
        }

        // 11. Value List Items (GH_ValueList)
        var listItems = chunk.Descendants("chunk")
            .Where(c => c.Attribute("name")?.Value == "ListItems")
            .SelectMany(c => c.Descendants("item"))
            .Where(i => i.Attribute("name")?.Value == "Name")
            .Select(i => i.Value?.Trim())
            .ToList();

        if (listItems.Any())
        {
            result["ListItems"] = string.Join(", ", listItems);
        }

        // 10. Generic fallback: capture ALL items that look like data
        // This ensures we don't miss custom component properties
        var allItems = chunk.Descendants("item")
            .Where(i => {
                var name = i.Attribute("name")?.Value;
                var val = i.Value?.Trim();
                return !string.IsNullOrEmpty(name) && 
                       !string.IsNullOrEmpty(val) &&
                       !result.ContainsKey(name) && // Don't duplicate
                       name != "InstanceGuid" && // Skip metadata
                       name != "Name" &&
                       name != "NickName" &&
                       name != "Description" &&
                       name != "Pivot" &&
                       name != "Source"; // Skip connection data
            });

        foreach (var item in allItems)
        {
            var name = item.Attribute("name")?.Value;
            var val = item.Value?.Trim();
            if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(val))
            {
                // Special handling for Bounds to normalize it
                if (name == "Bounds" && val.Contains("{") && val.Contains("}"))
                {
                    // Try to normalize format like {X=10, Y=20, Width=100, Height=50}
                    // This is a heuristic to prevent false positives due to formatting differences
                    try 
                    {
                        // Remove braces and split
                        var parts = val.Trim('{', '}').Split(',');
                        var normalizedParts = parts.Select(p => {
                            var kv = p.Split('=');
                            if (kv.Length == 2)
                            {
                                var k = kv[0].Trim();
                                var v = kv[1].Trim();
                                if (double.TryParse(v, out var d))
                                {
                                    return $"{k}={d:0.##}"; // Normalize to 2 decimal places
                                }
                                return $"{k}={v}";
                            }
                            return p.Trim();
                        }).OrderBy(s => s); // Sort keys to ignore order changes
                        
                        val = "{" + string.Join(", ", normalizedParts) + "}";
                    }
                    catch 
                    {
                        // If parsing fails, keep original
                    }
                }

                // Limit value length for display
                if (val.Length > 1000)
                {
                    val = val.Substring(0, 1000) + "... (truncated)";
                }
                result[name] = val;
            }
        }

        return result;
    }
}
