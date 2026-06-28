using System.Text.Json.Serialization;
using DevTools.Logging;

namespace DevTools.Daemon.Contracts;

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
    [property: JsonPropertyName("bridgeConnected")] bool BridgeConnected);

[UsedImplicitly]
public sealed record OpenModelResult(
    [property: JsonPropertyName("hostApp")]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp HostApp,
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("languageCode")] string? LanguageCode,
    [property: JsonPropertyName("filePath")] string FilePath,
    [property: JsonPropertyName("bridgeConnected")] bool BridgeConnected);

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
