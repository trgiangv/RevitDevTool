using System.Text.Json.Serialization;
using DevTools.Logging;

namespace DevTools.Daemon.Contracts;

[UsedImplicitly]
public sealed record LaunchHostResult(
    [property: JsonPropertyName(IpcPropertyNames.HostApp)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp HostApp,
    [property: JsonPropertyName(IpcPropertyNames.ProcessId)] int ProcessId,
    [property: JsonPropertyName(IpcPropertyNames.Version)] string? Version,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName(IpcPropertyNames.Arguments)] string? Arguments,
    [property: JsonPropertyName(McpPropertyNames.LanguageCode)] string? LanguageCode,
    [property: JsonPropertyName(IpcPropertyNames.BridgeConnected)] bool BridgeConnected);

[UsedImplicitly]
public sealed record OpenModelResult(
    [property: JsonPropertyName(IpcPropertyNames.HostApp)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp HostApp,
    [property: JsonPropertyName(IpcPropertyNames.ProcessId)] int ProcessId,
    [property: JsonPropertyName(IpcPropertyNames.Version)] string? Version,
    [property: JsonPropertyName(McpPropertyNames.LanguageCode)] string? LanguageCode,
    [property: JsonPropertyName(McpPropertyNames.FilePath)] string FilePath,
    [property: JsonPropertyName(IpcPropertyNames.BridgeConnected)] bool BridgeConnected);

[UsedImplicitly]
public sealed record ConnectedInstanceEntry(
    [property: JsonPropertyName(IpcPropertyNames.HostApp)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp? HostApp,
    [property: JsonPropertyName(IpcPropertyNames.ProcessId)] int ProcessId,
    [property: JsonPropertyName(IpcPropertyNames.VersionNumber)] string? VersionNumber);

[UsedImplicitly]
public sealed record DiscoveredPipeEntry(
    [property: JsonPropertyName(IpcPropertyNames.PipeName)] string PipeName,
    [property: JsonPropertyName(IpcPropertyNames.HostApp)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp? HostApp);

[UsedImplicitly]
public sealed record ListInstancesResult(
    [property: JsonPropertyName("connectedInstances")] IReadOnlyList<ConnectedInstanceEntry> ConnectedInstances,
    [property: JsonPropertyName("discoveredPipes")] IReadOnlyList<DiscoveredPipeEntry> DiscoveredPipes,
    [property: JsonPropertyName("totalConnected")] int TotalConnected,
    [property: JsonPropertyName("totalDiscovered")] int TotalDiscovered);
