using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Ipc;

/// <summary>
/// Structured error detail carried inside a <see cref="BridgeMessage"/> error response.
/// Mirrors the shape of JSON-RPC error objects so the Daemon routing layer can
/// forward meaningful error codes and optional data to external MCP clients.
/// </summary>
public sealed class BridgeError
{
    [JsonPropertyName(IpcPropertyNames.Code)]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName(IpcPropertyNames.Message)]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName(IpcPropertyNames.Data)]
    public JsonElement? Data { get; init; }
}

public sealed class BridgeMessage
{
    public const string TypeRequest = "request";
    public const string TypeResponse = "response";
    public const string TypeNotification = "notification";

    [JsonPropertyName(IpcPropertyNames.Type)]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName(IpcPropertyNames.Id)]
    public string? Id { get; init; }

    [JsonPropertyName(IpcPropertyNames.Method)]
    public string? Method { get; init; }

    [JsonPropertyName(IpcPropertyNames.Params)]
    public JsonElement? Params { get; init; }

    [JsonPropertyName(IpcPropertyNames.Result)]
    public JsonElement? Result { get; init; }

    [JsonPropertyName(IpcPropertyNames.IsError)]
    public bool IsError { get; init; }

    [JsonPropertyName(IpcPropertyNames.ErrorMessage)]
    public string? ErrorMessage { get; init; }

    [JsonPropertyName(IpcPropertyNames.Error)]
    public BridgeError? ErrorDetail { get; init; }

    public static BridgeMessage Request(string id, string method, JsonElement? @params = null) =>
        new() { Type = TypeRequest, Id = id, Method = method, Params = @params };

    public static BridgeMessage Response(string id, JsonElement? result) =>
        new() { Type = TypeResponse, Id = id, Result = result };

    public static BridgeMessage Error(string id, string code, string message, JsonElement? data = null) =>
        new()
        {
            Type = TypeResponse,
            Id = id,
            IsError = true,
            ErrorMessage = message,
            ErrorDetail = new BridgeError { Code = code, Message = message, Data = data }
        };

    public static BridgeMessage Notification(string method, JsonElement? @params = null) =>
        new() { Type = TypeNotification, Method = method, Params = @params };
}
