using System.Text.Json.Serialization;
using HopTracer.Web.Models;
using HopTracer.Core.Models;

namespace HopTracer.Web.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DiffResponse))]
[JsonSerializable(typeof(DiffMeta))]
[JsonSerializable(typeof(DiffDiagnostic))]
[JsonSerializable(typeof(NodeRiskFinding))]
[JsonSerializable(typeof(DiffRiskSummary))]
[JsonSerializable(typeof(DiffStageTiming))]
public partial class AppJsonContext : JsonSerializerContext
{
}
