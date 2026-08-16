using System.Text.Json.Serialization;
using DevTools.Ipc;
using DevTools.Hosting;
using DevTools.Utilities.Hosting;

namespace DevTools.Mcp.Server.Contracts;

[UsedImplicitly]
public sealed record LaunchHostResult(
    [property: JsonPropertyName(IpcPropertyNames.HostApp)]
    [property: JsonConverter(typeof(JsonStringEnumConverter<HostApp>))]
    HostApp HostApp,
    [property: JsonPropertyName(IpcPropertyNames.ProcessId)] int ProcessId,
    [property: JsonPropertyName(IpcPropertyNames.Version)] string? Version,
    [property: JsonPropertyName("path")] string? Path,
    [property: JsonPropertyName(IpcPropertyNames.Arguments)] string? Arguments,
    [property: JsonPropertyName("languageCode")] string? LanguageCode,
    [property: JsonPropertyName(IpcPropertyNames.BridgeConnected)] bool BridgeConnected,
    [property: JsonPropertyName("dialogResult")] StartupDialogResolverResult? DialogResult = null);

[UsedImplicitly]
public sealed record ConnectedInstanceEntry(
    [property: JsonPropertyName(IpcPropertyNames.HostApp)] string? HostApp,
    [property: JsonPropertyName(IpcPropertyNames.ProcessId)] int ProcessId,
    [property: JsonPropertyName(IpcPropertyNames.VersionNumber)] string? VersionNumber);

[UsedImplicitly]
public sealed record DiscoveredPipeEntry(
    [property: JsonPropertyName(IpcPropertyNames.PipeName)] string PipeName,
    [property: JsonPropertyName(IpcPropertyNames.HostApp)] string? HostApp);

[UsedImplicitly]
public sealed record ListInstancesResult(
    [property: JsonPropertyName("connectedInstances")] IReadOnlyList<ConnectedInstanceEntry> ConnectedInstances,
    [property: JsonPropertyName("discoveredPipes")] IReadOnlyList<DiscoveredPipeEntry> DiscoveredPipes,
    [property: JsonPropertyName("totalConnected")] int TotalConnected,
    [property: JsonPropertyName("totalDiscovered")] int TotalDiscovered);
