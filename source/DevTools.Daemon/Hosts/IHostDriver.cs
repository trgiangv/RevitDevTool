using DevTools.Daemon.Contracts;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Logging;

namespace DevTools.Daemon.Hosts;

/// <summary>Owns daemon-side launch and offline file metadata behavior for one host family.</summary>
public interface IHostDriver
{
    string HostId { get; }
    IReadOnlySet<HostApp> SupportedHostApps { get; }
    IReadOnlySet<string> FileExtensions { get; }
    bool SupportsVersion(string version);
    Task<HostLaunchResult> LaunchAsync(HostLaunchRequest request, CancellationToken ct);
    Task<FileInfoResult> ReadFileInfoAsync(string filePath, CancellationToken ct);
}

/// <summary>Immutable input that keeps host-product and Revit language selection request-scoped.</summary>
public sealed record HostLaunchRequest(
    HostApp RequestedHostApp,
    string? VersionNumber,
    string? LanguageCode,
    string? FilePath);

/// <summary>Internal launch state used by product-neutral MCP tools to preserve their existing payloads.</summary>
public sealed record HostLaunchResult(
    HostApp HostApp,
    int ProcessId,
    string Version,
    string ExePath,
    string? LanguageCode,
    IReadOnlyList<string> Arguments,
    Task<StartupDialogResolverResult>? DialogTask);

public sealed class HostDriverException(string message) : Exception(message);
