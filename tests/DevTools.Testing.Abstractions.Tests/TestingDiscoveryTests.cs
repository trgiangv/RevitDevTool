using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class TestingDiscoveryTests
{
    private static readonly Lock Gate = new();

    [Fact]
    public void Register_assigns_provider_and_mapper_together()
    {
        lock (Gate)
        {
            var previous = TestingDiscovery.Current;
            try
            {
                TestingDiscovery.Clear();
                var discoverer = new StubDiscoverer();
                TestingDiscovery.Register(discoverer, PassThroughRunMapper.Instance);

                Assert.Same(discoverer, TestingDiscovery.Provider);
                Assert.Same(PassThroughRunMapper.Instance, TestingDiscovery.RunMapper);
                Assert.NotNull(TestingDiscovery.Current);
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
            () => TestingDiscovery.Register(new StubDiscoverer(), null!));
    }

    [Fact]
    public void Register_rejects_a_different_bridge()
    {
        lock (Gate)
        {
            var previous = TestingDiscovery.Current;
            try
            {
                TestingDiscovery.Clear();
                TestingDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance);
                Assert.Throws<InvalidOperationException>(
                    () => TestingDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance));
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
            var previous = TestingDiscovery.Current;
            try
            {
                TestingDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance);
                TestingDiscovery.Clear();
                Assert.Null(TestingDiscovery.Provider);
                Assert.Null(TestingDiscovery.RunMapper);
                Assert.Null(TestingDiscovery.Current);
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    static void Restore(TestingDiscoveryBridge? previous)
    {
        TestingDiscovery.Clear();
        if (previous is not null)
            TestingDiscovery.Register(previous.Discoverer, previous.RunMapper);
    }

    sealed class StubDiscoverer : ITestDiscoverer
    {
        public IReadOnlyList<TestDiscoveredTest> Discover(
            string assemblyPath,
            TestSelection selection) =>
            [];
    }
}
