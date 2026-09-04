using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Mcp.Server.Tests;

internal sealed class TestMcpServerDependencies
{
    public ILoggerFactory LoggerFactory { get; } = NullLoggerFactory.Instance;
    public IServiceScopeFactory ScopeFactory { get; } = new PassthroughScopeFactory();

    private sealed class PassthroughScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new PassthroughScope();

        private sealed class PassthroughScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
            public void Dispose() { }
        }
    }
}
