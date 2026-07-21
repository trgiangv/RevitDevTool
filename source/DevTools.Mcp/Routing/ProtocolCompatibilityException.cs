namespace DevTools.Mcp.Routing;

public sealed class ProtocolCompatibilityException(string code, string message)
    : InvalidOperationException(message)
{
    public string Code { get; } = code;
}
