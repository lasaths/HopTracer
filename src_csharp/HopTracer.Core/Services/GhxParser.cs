using System.Xml.Linq;
using HopTracer.Core.Models;
using Microsoft.Extensions.Logging;

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
            doc = XDocument.Load(path);
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
            
            if (xElem != null && double.TryParse(xElem.Value, out var xVal))
                x = xVal;
            if (yElem != null && double.TryParse(yElem.Value, out var yVal))
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
    /// Best-effort extraction of component values/code text for later diffing.
    /// </summary>
    private Dictionary<string, string> ExtractValues(XElement chunk)
    {
        var result = new Dictionary<string, string>();

        // Common Grasshopper parameter payloads
        var candidates = new[] { "Value", "Number", "Text", "String", "Expression", "Code", "Script" };
        foreach (var name in candidates)
        {
            var val = GetValue(chunk, name);
            if (!string.IsNullOrEmpty(val))
            {
                result[name] = val;
            }
        }

        return result;
    }
}
