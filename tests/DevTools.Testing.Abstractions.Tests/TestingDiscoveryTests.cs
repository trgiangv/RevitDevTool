using DevTools.Testing.Abstractions;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class TestingDiscoveryTests
{
    private static readonly Lock Gate = new();

    [TestMethod]
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

                Assert.AreSame(discoverer, TestingDiscovery.Provider);
                Assert.AreSame(PassThroughRunMapper.Instance, TestingDiscovery.RunMapper);
                Assert.IsNotNull(TestingDiscovery.Current);
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    [TestMethod]
    public void Register_rejects_a_null_mapper()
    {
        Assert.ThrowsExactly<ArgumentNullException>(
            () => TestingDiscovery.Register(new StubDiscoverer(), null!));
    }

    [TestMethod]
    public void Register_rejects_a_different_bridge()
    {
        lock (Gate)
        {
            var previous = TestingDiscovery.Current;
            try
            {
                TestingDiscovery.Clear();
                TestingDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance);
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => TestingDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance));
            }
            finally
            {
                Restore(previous);
            }
        }
    }

    [TestMethod]
    public void Clear_drops_both_registrations()
    {
        lock (Gate)
        {
            var previous = TestingDiscovery.Current;
            try
            {
                TestingDiscovery.Register(new StubDiscoverer(), PassThroughRunMapper.Instance);
                TestingDiscovery.Clear();
                Assert.IsNull(TestingDiscovery.Provider);
                Assert.IsNull(TestingDiscovery.RunMapper);
                Assert.IsNull(TestingDiscovery.Current);
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
