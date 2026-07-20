using System.Text.Json.Serialization;
using DevTools.Daemon.Mcp.Tools.Utils;
using DevTools.Logging;

namespace DevTools.Daemon.Contracts;

public static class LaunchHostStatus
{
    public const string ConnectedCatalogReady = "connected_catalog_ready";
    public const string ConnectedCatalogPending = "connected_catalog_pending";
    public const string LaunchFailed = "launch_failed";
    public const string ConnectionTimeout = "connection_timeout";
}

[UsedImplicitly]
public sealed record LaunchHostResult(
    [property: JsonPropertyName("hostApp")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp HostApp,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName("arguments")] string? Arguments,
    [property: JsonPropertyName("languageCode")] string? LanguageCode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("bridgeConnected")] bool BridgeConnected,
    [property: JsonPropertyName("message")] string? Message = null,
    [property: JsonPropertyName("dialogResult")] StartupDialogResolverResult? DialogResult = null);

[UsedImplicitly]
public sealed record OpenModelResult(
    [property: JsonPropertyName("hostApp")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp HostApp,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("languageCode")] string? LanguageCode,
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("bridgeConnected")] bool BridgeConnected,
    [property: JsonPropertyName("dialogResult")] StartupDialogResolverResult? DialogResult = null);

[UsedImplicitly]
public sealed record ConnectedInstanceEntry(
    [property: JsonPropertyName("hostApp")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp? HostApp,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("versionNumber")] string? VersionNumber);

[UsedImplicitly]
public sealed record DiscoveredPipeEntry(
    [property: JsonPropertyName("pipeName")] string PipeName,
    [property: JsonPropertyName("hostApp")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp? HostApp);

[UsedImplicitly]
public sealed record ListInstancesResult(
    [property: JsonPropertyName("connectedInstances")] IReadOnlyList<ConnectedInstanceEntry> ConnectedInstances,
    [property: JsonPropertyName("discoveredPipes")] IReadOnlyList<DiscoveredPipeEntry> DiscoveredPipes,
    [property: JsonPropertyName("totalConnected")] int TotalConnected,
    [property: JsonPropertyName("totalDiscovered")] int TotalDiscovered);
