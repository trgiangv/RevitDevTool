using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class HostTestDiscoveryTests
{
    static readonly object Gate = new();

    [Fact]
    public void Register_assigns_provider_and_mapper_together()
    {
        lock (Gate)
        {
            var previous = HostTestDiscovery.Current;
            try
            {
                HostTestDiscovery.Clear();
                var discoverer = new StubDiscoverer();
                HostTestDiscovery.Register(discoverer, PassThroughRunMapper.Instance);

                Assert.Same(discoverer, HostTestDiscovery.Provider);
                Assert.Same(PassThroughRunMapper.Instance, HostTestDiscovery.RunMapper);
                Assert.NotNull(HostTestDiscovery.Current);
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    [Fact]
    public void Register_rejects_a_null_mapper()
    {
        Assert.Throws<ArgumentNullException>(
            () => HostTestDiscovery.Register(new StubDiscoverer(), null!));
    }

    [Fact]
    public void Register_rejects_a_different_bridge()
    {
        lock (Gate)
        {
            var previous = HostTestDiscovery.Current;
            try
            {
                HostTestDiscovery.Clear();
                HostTestDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance);
                Assert.Throws<InvalidOperationException>(
                    () => HostTestDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance));
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    [Fact]
    public void Clear_drops_both_registrations()
    {
        lock (Gate)
        {
            var previous = HostTestDiscovery.Current;
            try
            {
                HostTestDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance);
                HostTestDiscovery.Clear();
                Assert.Null(HostTestDiscovery.Provider);
                Assert.Null(HostTestDiscovery.RunMapper);
                Assert.Null(HostTestDiscovery.Current);
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    static void Restore(HostTestFrameworkBridge? previous)
    {
        HostTestDiscovery.Clear();
        if (previous is not null)
            HostTestDiscovery.Register(previous.Discoverer, previous.RunMapper);
    }

    sealed class StubDiscoverer : IHostTestDiscoverer
    {
        public IReadOnlyList<TestingDiscoveredTest> Discover(
            string assemblyPath,
            TestingSelection selection) =>
            [];
    }
}
