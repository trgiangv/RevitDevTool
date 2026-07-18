namespace DevTools.Mcp.Routing;

public sealed record HostInstanceDescriptor(
    int ProcessId,
    string HostApp,
    string VersionNumber,
    string PipeName);
