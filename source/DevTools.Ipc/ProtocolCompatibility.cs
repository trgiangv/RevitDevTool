namespace DevTools.Ipc;

/// <summary>
/// Coordinated minimum versions for the unified MCP 4.0 release set.
/// Keep <c>docs/MCP/compatibility.md</c> aligned when these change.
/// </summary>
public static class ProtocolCompatibility
{
    /// <summary>Current in-host MCP protocol version advertised at initialize.</summary>
    public const string HostProtocolVersion = "4.0.0";

    /// <summary>Minimum compatible in-host MCP protocol version.</summary>
    public const string MinHostProtocolVersion = "4.0.0";

    /// <summary>Minimum compatible DevTools.Daemon product version.</summary>
    public const string MinDaemonVersion = "4.0.0";

    /// <summary>Minimum compatible revitdevtool_pytest plugin version.</summary>
    public const string MinPytestPluginVersion = "0.4.0";

    /// <summary>Minimum compatible McpGateway product version.</summary>
    public const string MinGatewayVersion = "2.0.0";

    /// <summary>Current McpGateway product version returned on tunnel registration.</summary>
    public const string GatewayVersion = "2.0.0";

    public static bool IsAtLeast(string? actual, string minimum)
    {
        if (!TryParse(actual, out var actualVersion) || !TryParse(minimum, out var minimumVersion))
            return false;

        return actualVersion >= minimumVersion;
    }

    public static string FormatMismatch(string component, string? actual, string required) =>
        $"protocol_version_mismatch: {component} version {actual ?? "<missing>"} is below required {required}";

    private static bool TryParse(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value!.Trim();
        var plusIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
            normalized = normalized[..plusIndex];

        if (!Version.TryParse(normalized, out var parsed))
            return false;

        version = parsed;
        return true;
    }
}
