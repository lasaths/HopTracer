
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System.IO;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: dotnet script scripts/ReproParser.cs <path-to-ghx>");
    return;
}

var parser = new GhxParser(NullLogger<GhxParser>.Instance);
var path = Path.GetFullPath(args[0]);

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
