using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevTools.Daemon.Hosting;

/// <summary>Versioned, session-scoped gateway tunnel envelope. MCP payloads stay opaque.</summary>
public sealed record GatewayTunnelEnvelope(
    [property: JsonPropertyName("v")] int V,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("session_id")] string? SessionId = null,
    [property: JsonPropertyName("message")] JsonElement? Message = null,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("machine_id")] string? MachineId = null,
    [property: JsonPropertyName("machine_name")] string? MachineName = null,
    [property: JsonPropertyName("host_apps")] IReadOnlyList<string>? HostApps = null,
    [property: JsonPropertyName("connection_generation")] long? ConnectionGeneration = null)
{
    public const int ProtocolVersion = 2;
    public const string Register = "register";
    public const string Registered = "registered";
    public const string Heartbeat = "heartbeat";
    public const string SessionOpen = "session.open";
    public const string SessionOpened = "session.opened";
    public const string McpMessage = "mcp.message";
    public const string SessionClose = "session.close";
    public const string SessionClosed = "session.closed";
    public const string UnknownSession = "unknown_session";

    public static GatewayTunnelEnvelope Closed(string sessionId, string reason) =>
        new(ProtocolVersion, SessionClosed, sessionId, Reason: reason);

    public static bool TryParse(JsonElement value, out GatewayTunnelEnvelope? envelope, out string? error)
    {
        envelope = null;
        error = "invalid_tunnel_frame";
        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("v", out var version) ||
            !version.TryGetInt32(out var parsedVersion))
            return false;

        if (parsedVersion != ProtocolVersion)
        {
            error = "unsupported_tunnel_protocol";
            return false;
        }

        if (!value.TryGetProperty("type", out var typeValue) ||
            typeValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(typeValue.GetString()))
            return false;

        var type = typeValue.GetString()!;
        if (type is not (Register or Registered or Heartbeat or SessionOpen or SessionOpened or McpMessage or SessionClose or SessionClosed))
            return false;

        var sessionId = ReadString(value, "session_id");
        if ((type is SessionOpen or SessionOpened or McpMessage or SessionClose or SessionClosed) || sessionId is not null)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return false;
        }

        JsonElement? message = null;
        if (type == McpMessage)
        {
            if (!value.TryGetProperty("message", out var messageValue) || messageValue.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return false;
            message = messageValue.Clone();
        }

        var machineId = ReadString(value, "machine_id");
        var machineName = ReadString(value, "machine_name");
        IReadOnlyList<string>? hostApps = null;
        if (type is Register or Heartbeat)
        {
            if ((type == Register && (string.IsNullOrWhiteSpace(machineId) || string.IsNullOrWhiteSpace(machineName))) ||
                !value.TryGetProperty("host_apps", out var appsValue) || appsValue.ValueKind != JsonValueKind.Array)
                return false;
            hostApps = appsValue.EnumerateArray().Select(app => app.GetString()!).ToArray();
            if (hostApps.Any(string.IsNullOrWhiteSpace)) return false;
        }

        long? generation = null;
        if (value.TryGetProperty("connection_generation", out var generationValue))
        {
            if (!generationValue.TryGetInt64(out var parsedGeneration)) return false;
            generation = parsedGeneration;
        }
        if (type == Registered && generation is not > 0) return false;

        envelope = new GatewayTunnelEnvelope(
            parsedVersion, type, sessionId, message, ReadString(value, "reason"), machineId, machineName, hostApps, generation);
        error = null;
        return true;
    }

    private static string? ReadString(JsonElement value, string propertyName) =>
        value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
