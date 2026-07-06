namespace DevTools.Ipc;

/// <summary>
/// Formatting, parsing, and display helpers for DevTools host Named Pipes.
/// Pipe name format: <c>DevTools_{Host}_{Version}_{PID}</c>
/// </summary>
public static class HostPipeName
{
    private const char Separator = '_';
    private const int MinSegments = 4; // prefix, host, version, pid

    /// <summary>Build a pipe name from components.</summary>
    public static string Format(string host, string version, int processId)
        => $"{DaemonConstants.PipePrefix}{Separator}{host}{Separator}{version}{Separator}{processId}";

    /// <summary>
    /// Parse a full pipe name into host, version, and PID.
    /// Returns <c>false</c> when the name does not match the DevTools pattern.
    /// </summary>
    public static bool TryParse(string pipeName, out string host, out string version, out int pid)
    {
        host = string.Empty;
        version = string.Empty;
        pid = 0;

        if (!pipeName.StartsWith(DaemonConstants.PipePrefix + Separator, StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = pipeName.Split(Separator);
        if (parts.Length < MinSegments || !int.TryParse(parts[^1], out pid))
            return false;

        host = parts[^3];
        version = parts[^2];
        return true;
    }

    /// <summary>
    /// Extract just the host segment (e.g. "Revit") from a full pipe name.
    /// Returns <c>null</c> when the name does not match.
    /// </summary>
    public static string? ExtractHost(string pipeName)
        => TryParse(pipeName, out var host, out _, out _) ? host : null;

    /// <summary>
    /// Strip the <c>DevTools_</c> vendor prefix for UI display.
    /// Returns the input unchanged when it does not carry the prefix.
    /// </summary>
    public static string ToDisplayName(string pipeName)
    {
        var prefix = DaemonConstants.PipePrefix + Separator;
        return pipeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? pipeName.Substring(prefix.Length)
            : pipeName;
    }
}
