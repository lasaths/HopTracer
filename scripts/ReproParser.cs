
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System.IO;

var parser = new GhxParser(NullLogger<GhxParser>.Instance);
var path = Path.GetFullPath("Tests/data/SampleDefinition.ghx");

Console.WriteLine($"Parsing {path}...");
var graph = parser.Parse(path);

Console.WriteLine($"Nodes: {graph.Nodes.Count}");
Console.WriteLine($"Edges: {graph.Edges.Count}");

foreach (var edge in graph.Edges)
{
    var linkBroke = !graph.Nodes.ContainsKey(edge.Source);
    Console.WriteLine($"Edge: {edge.Source} -> {edge.Target} (Broken Source? {linkBroke})");
    if (linkBroke) {
        Console.WriteLine($"  FAIL: Edge source {edge.Source} is not a known node ID.");
    }
}
