using System.Globalization;

namespace DevTools.Ipc;

/// <summary>
/// Formatting, parsing, and display helpers for DevTools host Named Pipes.
/// Pipe name format: <c>DevTools_{Host}_{Version}_{PID}</c>
/// </summary>
public static class HostPipeName
{
    private const char Separator = '_';

    /// <summary>Build a pipe name from components.</summary>
    public static string Format(string host, string version, int processId)
    {
        ValidateSegment(host, nameof(host));
        ValidateSegment(version, nameof(version));
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));

        return string.Concat(
            DaemonConstants.PipePrefix,
            Separator,
            host,
            Separator,
            version,
            Separator,
            processId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Parse a full pipe name into host, version, and PID.
    /// Returns <c>false</c> when the name does not match the DevTools pattern.
    /// </summary>
    public static bool TryParse(string pipeName, out string host, out string version, out int pid)
    {
        host = string.Empty;
        version = string.Empty;
        pid = 0;

        if (pipeName is null)
            return false;

        var parts = pipeName.Split(Separator);
        if (parts.Length != 4 ||
            !parts[0].Equals(DaemonConstants.PipePrefix, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(parts[1]) ||
            string.IsNullOrWhiteSpace(parts[2]) ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out pid) ||
            pid <= 0)
            return false;

        host = parts[1];
        version = parts[2];
        return true;
    }

    private static void ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.IndexOf(Separator) >= 0)
            throw new ArgumentException("Pipe identity segments must be nonempty and cannot contain '_'.", parameterName);
    }

    /// <summary>
    /// Extract just the host segment (e.g. "Revit") from a full pipe name.
    /// Returns <c>null</c> when the name does not match.
    /// </summary>
    public static string? ExtractHost(string pipeName)
        => TryParse(pipeName, out var host, out _, out _) ? host : null;

    /// <summary>Compare host segments for discovery/matching (case-insensitive).</summary>
    public static bool HostSegmentEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>Compare version segments for discovery/matching (case-insensitive).</summary>
    public static bool VersionSegmentEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Compare parsed identity segments for discovery/matching.
    /// Host and version use case-insensitive comparison; PID is exact.
    /// </summary>
    public static bool IdentityEquals(
        string host,
        string version,
        int processId,
        string otherHost,
        string otherVersion,
        int otherProcessId) =>
        HostSegmentEquals(host, otherHost) &&
        VersionSegmentEquals(version, otherVersion) &&
        processId == otherProcessId;

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
