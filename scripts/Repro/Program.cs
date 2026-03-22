using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run --project scripts/Repro -- <base-ghx> <modified-ghx>");
    return;
}

var parser = new GhxParser(NullLogger<GhxParser>.Instance);
var pathBase = Path.GetFullPath(args[0]);
var pathModified = Path.GetFullPath(args[1]);

void PrintClusterProps(string title, string path)
{
    Console.WriteLine($"\n--- {title} ---");
    var graph = parser.Parse(path);
    foreach (var node in graph.Nodes.Values)
    {
        if (node.Properties.ContainsKey("IsCluster"))
        {
            Console.WriteLine($"Node: {node.Nickname}");
            Console.WriteLine($"  IsCluster: {node.Properties["IsCluster"]}");
            Console.WriteLine($"  ClusterSize: {node.Properties["ClusterSize"]}");
            Console.WriteLine($"  ClusterHash: {node.Properties["ClusterHash"]}");
        }
    }
}

PrintClusterProps(Path.GetFileName(pathBase), pathBase);
PrintClusterProps(Path.GetFileName(pathModified), pathModified);
