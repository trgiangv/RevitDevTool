using System.Text.Json.Serialization;

namespace DevTools.Ipc;

[JsonSerializable(typeof(BridgeMessage))]
[JsonSerializable(typeof(BridgeError))]
[JsonSerializable(typeof(InstanceInfo))]
[JsonSerializable(typeof(ProtocolRange))]
[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public sealed partial class IpcJsonContext : JsonSerializerContext;
