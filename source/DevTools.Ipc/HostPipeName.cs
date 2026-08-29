namespace DevTools.Ipc;

/// <summary>
/// Formatting, parsing, and display helpers for DevTools host Named Pipes.
/// Pytest pipe: <c>DevTools_{Host}_{Version}_{PID}</c> (pytest/control <see cref="BridgeMessage"/>).
/// MCP pipe: <c>DevToolsMcp_{Host}_{Version}_{PID}</c> (SDK JSON-RPC).
/// </summary>
public static class HostPipeName
{
    private const char Separator = '_';
    private const int MinSegments = 4; // prefix, host, version, pid

    /// <summary>Build a pytest/control pipe name.</summary>
    public static string FormatTest(string host, string version, int processId)
        => Format(DaemonConstants.TestPipePrefix, host, version, processId);

    /// <summary>Build an SDK MCP pipe name.</summary>
    public static string FormatMcp(string host, string version, int processId)
        => Format(DaemonConstants.McpPipePrefix, host, version, processId);

    /// <summary>Derive the MCP pipe name from a pytest or MCP pipe name, or return null if invalid.</summary>
    public static string? ToMcpPipeName(string pytestOrMcpPipeName)
    {
        return TryParse(pytestOrMcpPipeName, out var host, out var version, out var pid) ? FormatMcp(host, version, pid) : null;
    }

    /// <summary>
    /// Parse a full pipe name (Test or MCP) into host, version, and PID.
    /// Returns <c>false</c> when the name does not match either DevTools pattern.
    /// </summary>
    public static bool TryParse(string pipeName, out string host, out string version, out int pid)
    {
        host = string.Empty;
        version = string.Empty;
        pid = 0;

        if (!IsCorrectPipe(pipeName))
            return false;

        var parts = pipeName.Split(Separator);
        if (parts.Length < MinSegments || !int.TryParse(parts[^1], out pid))
            return false;

        host = parts[^3];
        version = parts[^2];
        return true;
    }

    /// <summary>True when the name is an SDK MCP pipe.</summary>
    public static bool IsMcpPipe(string pipeName)
        => pipeName.StartsWith(DaemonConstants.McpPipePrefix + Separator, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when the name is a test/control pipe.</summary>
    public static bool IsTestPipe(string pipeName)
        => pipeName.StartsWith(DaemonConstants.TestPipePrefix + Separator, StringComparison.OrdinalIgnoreCase)
           && !IsMcpPipe(pipeName);

    /// <summary>
    /// Extract just the host segment (e.g. "Revit") from a full pipe name.
    /// Returns <c>null</c> when the name does not match.
    /// </summary>
    public static string? ExtractHost(string pipeName)
        => TryParse(pipeName, out var host, out _, out _) ? host : null;

    private static string Format(string prefix, string host, string version, int processId)
        => $"{prefix}{Separator}{host}{Separator}{version}{Separator}{processId}";

    private static bool IsCorrectPipe(string pipeName)
    {
        if (pipeName.StartsWith(DaemonConstants.McpPipePrefix + Separator, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pipeName.StartsWith(DaemonConstants.TestPipePrefix + Separator, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
