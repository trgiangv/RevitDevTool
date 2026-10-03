using DevTools.Execution.Abstractions;
using DevTools.Execution.External.Mcp.Backends;
using DevTools.Mcp.Discovery;
using DevTools.Mcp.Isolation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class DotnetSourceTests
{
    [TestMethod]
    public void SourceKind_IsDotnet()
    {
        var backend = CreateBackend();
        Assert.AreEqual(ExecutionMode.Dotnet, backend.SourceKind);
    }

    [TestMethod]
    public void ClearCaches_DoesNotThrow()
    {
        var backend = CreateBackend();
        backend.ClearCaches();
        backend.ClearCaches();
    }

    private static DotnetSource CreateBackend()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var provider = services.BuildServiceProvider();
        return new DotnetSource(
            provider,
            new DotnetMethodResolver(
                new McpToolsetContextManager(NullLogger<McpToolsetContextManager>.Instance),
                NullLogger<DotnetMethodResolver>.Instance));
    }
}
