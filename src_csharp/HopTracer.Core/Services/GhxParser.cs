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
        var edges = new List<(string, string)>();

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
                var sources = ParseSources(chunk);
                foreach (var srcGuid in sources)
                {
                    edges.Add((srcGuid, node.Id));
                }
            }
        }

        _logger.LogInformation("Parsed graph with {NodeCount} nodes and {EdgeCount} edges from {Path}", nodes.Count, edges.Count, path);
        return new Graph { Nodes = nodes, Edges = edges };
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

        return new Node
        {
            Id = guid,
            Name = name,
            Nickname = nickname,
            X = x,
            Y = y
        };
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
}
