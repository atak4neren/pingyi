using System.Text.Json.Serialization;
using PingYi.Core;

namespace PingYi.Infrastructure;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class PingYiJsonContext : JsonSerializerContext;
