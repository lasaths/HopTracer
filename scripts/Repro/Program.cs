using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;

var parser = new GhxParser(NullLogger<GhxParser>.Instance);
var pathBase = Path.GetFullPath("Tests/data/Cluster_Base.ghx");
var pathModified = Path.GetFullPath("Tests/data/Cluster_Modified.ghx");

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

PrintClusterProps("Cluster_Base.ghx", pathBase);
PrintClusterProps("Cluster_Modified.ghx", pathModified);
