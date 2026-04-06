using HopTracer.Core.Models;

namespace HopTracer.Core.Services;

public interface IGhxParser
{
    Graph Parse(string path);
}

public interface IDiffer
{
    (List<Node> Nodes, List<Edge> Edges) Diff(Graph oldGraph, Graph newGraph);
    DiffComputation DiffDetailed(Graph oldGraph, Graph newGraph);
}

public interface IConverterService
{
    string ConvertGhToGhx(string inputPath, string? outputPath = null);
    bool TryCheckDependencies(out string message);
}
