using System.Text.Json.Serialization;
using HopTracer.Web.Models;
using HopTracer.Core.Models;

namespace HopTracer.Web.Serialization;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DiffResponse))]
[JsonSerializable(typeof(DiffMeta))]
public partial class AppJsonContext : JsonSerializerContext
{
}
