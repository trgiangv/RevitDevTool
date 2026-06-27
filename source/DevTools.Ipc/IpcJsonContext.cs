using System.Text.Json.Serialization;

namespace DevTools.Ipc;

[JsonSerializable(typeof(BridgeMessage))]
[JsonSerializable(typeof(BridgeError))]
[JsonSerializable(typeof(InstanceInfo))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class IpcJsonContext : JsonSerializerContext;
