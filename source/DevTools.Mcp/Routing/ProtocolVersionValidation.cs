using System.Text.Json;
using DevTools.Ipc;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Routing;

public static class ProtocolVersionValidation
{
    public static void RequireHostProtocolVersion(ServerCapabilities? capabilities, string pipeName)
    {
        if (!TryReadHostProtocolVersion(capabilities, out var actual))
        {
            throw new ProtocolCompatibilityException(
                "host_protocol_missing",
                $"{ProtocolCompatibility.FormatMismatch("host", actual ?? "unknown", ProtocolCompatibility.MinHostProtocolVersion)} ({pipeName}).");
        }

        if (!ProtocolCompatibility.IsAtLeast(actual!, ProtocolCompatibility.MinHostProtocolVersion))
        {
            throw new ProtocolCompatibilityException(
                "host_protocol_mismatch",
                $"{ProtocolCompatibility.FormatMismatch("host", actual!, ProtocolCompatibility.MinHostProtocolVersion)} ({pipeName}).");
        }
    }

    public static bool TryReadHostProtocolVersion(ServerCapabilities? capabilities, out string? version)
    {
        version = null;
        if (capabilities?.Experimental is null)
            return false;

        var experimental = JsonSerializer.SerializeToElement(capabilities.Experimental);
        if (!experimental.TryGetProperty("devtools", out var devtools)
            || !devtools.TryGetProperty("protocol", out var protocol)
            || !protocol.TryGetProperty("version", out var versionElement)
            || versionElement.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        version = versionElement.GetString();
        return !string.IsNullOrWhiteSpace(version);
    }
}
