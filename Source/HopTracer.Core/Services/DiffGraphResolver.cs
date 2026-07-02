using HopTracer.Core.Models;

namespace HopTracer.Core.Services;

public sealed class ResolvedEdge
{
    public string Source { get; set; } = string.Empty;
    public string SourcePort { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string TargetPort { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string SourceLabel { get; set; } = string.Empty;
    public string SourcePortName { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;
    public string TargetPortName { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string Wire { get; set; } = string.Empty;
}

public sealed class DiffGraphResolver
{
    private readonly Dictionary<string, string> _nodeLabels;
    private readonly Dictionary<string, (string NodeLabel, string PortLabel)> _portLabels;

    public DiffGraphResolver(IEnumerable<Node> nodes)
    {
        _nodeLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        _portLabels = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        foreach (var node in nodes)
        {
            _nodeLabels[node.Id] = NodeLabel(node);
            foreach (var port in node.Inputs.Concat(node.Outputs))
            {
                _portLabels[port.Id] = (_nodeLabels[node.Id], PortLabel(port));
            }
        }
    }

    public ResolvedEdge Resolve(Edge edge)
    {
        var sourceNode = LabelNode(edge.Source);
        var targetNode = LabelNode(edge.Target);
        var sourcePort = LabelPort(edge.SourcePort, sourceNode);
        var targetPort = LabelPort(edge.TargetPort, targetNode);
        var from = $"{sourceNode}.{sourcePort}";
        var to = $"{targetNode}.{targetPort}";

        return new ResolvedEdge
        {
            Source = edge.Source,
            SourcePort = edge.SourcePort,
            Target = edge.Target,
            TargetPort = edge.TargetPort,
            Status = edge.Status,
            SourceLabel = sourceNode,
            SourcePortName = sourcePort,
            TargetLabel = targetNode,
            TargetPortName = targetPort,
            From = from,
            To = to,
            Wire = $"{from} → {to}"
        };
    }

    public string LabelNode(string nodeId) =>
        _nodeLabels.TryGetValue(nodeId, out var label) ? label : ShortId(nodeId);

    private string LabelPort(string portId, string fallbackNodeLabel)
    {
        if (_portLabels.TryGetValue(portId, out var port))
        {
            return port.PortLabel;
        }

        return ShortId(portId);
    }

    private static string NodeLabel(Node node) =>
        string.IsNullOrWhiteSpace(node.Nickname) ? node.Name : node.Nickname;

    private static string PortLabel(Port port) =>
        string.IsNullOrWhiteSpace(port.Nickname) ? port.Name : port.Nickname;

    private static string ShortId(string id) =>
        id.Length <= 8 ? id : id[..8] + "…";
}
