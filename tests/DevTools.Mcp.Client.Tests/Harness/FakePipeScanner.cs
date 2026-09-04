using DevTools.Mcp.Client;

namespace DevTools.Mcp.Client.Tests.Harness;

internal sealed class FakePipeScanner : IMcpPipeScanner
{
    private IReadOnlyCollection<string> _pipes = [];

    public void SetPipes(params string[] pipes) => _pipes = pipes;

    public IReadOnlyCollection<string> Discover() => _pipes;
}
