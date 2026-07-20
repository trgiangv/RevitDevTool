namespace DevTools.Mcp.Routing;

public sealed class HostIdentityException(string code, string pipeName)
    : InvalidOperationException($"Host identity validation failed: {code} ({pipeName}).")
{
    public string Code { get; } = code;
    public string PipeName { get; } = pipeName;
}
