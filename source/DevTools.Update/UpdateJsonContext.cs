using DevTools.Update.Releases;
using System.Text.Json.Serialization;

namespace DevTools.Update;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    WriteIndented = true)]
[JsonSerializable(typeof(UpdateManifest))]
[JsonSerializable(typeof(Installations.Installation))]
internal sealed partial class UpdateJsonContext : JsonSerializerContext;
