using DevTools.Hosting;
using DevTools.Ipc;

namespace DevTools.Mcp.Server.Utils;

public static class HostAppParsing
{
    public static HostApp? FromPipeName(string pipeName)
    {
        var hostSegment = HostPipeName.ExtractHost(pipeName);
        if (hostSegment is null) return null;
        return Enum.TryParse<HostApp>(hostSegment, ignoreCase: true, out var app) ? app : null;
    }

    public static HostApp? ParseHostApp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Enum.TryParse<HostApp>(value, ignoreCase: true, out var app) ? app : null;
    }
}
