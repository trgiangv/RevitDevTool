using DevTools.Execution.Diagnostics;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class DebugPortLeaseTests
{
    [TestMethod]
    public void Acquire_EphemeralTwice_YieldsDistinctPorts()
    {
        using var first = DebugPortLease.Acquire(0);
        using var second = DebugPortLease.Acquire(0);

        Assert.AreNotEqual(0, first.Port);
        Assert.AreNotEqual(0, second.Port);
        Assert.AreNotEqual(first.Port, second.Port);
        Assert.IsFalse(first.IsPreferred);
        Assert.IsFalse(second.IsPreferred);
    }

    [TestMethod]
    public void Acquire_SamePortTwice_FallsBackToEphemeral()
    {
        using var first = DebugPortLease.Acquire(0);
        using var second = DebugPortLease.Acquire(first.Port);

        Assert.AreNotEqual(first.Port, second.Port);
        Assert.IsFalse(second.IsPreferred);
    }

    [TestMethod]
    public void Acquire_AfterDispose_CanReusePort()
    {
        int port;
        using (var first = DebugPortLease.Acquire(0))
            port = first.Port;

        using var again = DebugPortLease.Acquire(port);
        Assert.AreEqual(port, again.Port);
        Assert.IsTrue(again.IsPreferred);
    }

    [TestMethod]
    public void Endpoint_ReserveThenReleaseLease_KeepsPort()
    {
        using var endpoint = new DebugEndpoint();
        endpoint.Reserve(0);
        var port = endpoint.Port;
        Assert.AreNotEqual(0, port);

        Assert.AreEqual(port, endpoint.ReleaseLease());
        Assert.AreEqual(port, endpoint.Port);
        Assert.IsFalse(endpoint.IsListening);

        endpoint.MarkListening(port);
        Assert.IsTrue(endpoint.IsListening);
        Assert.AreEqual(port, endpoint.Port);
        Assert.IsFalse(endpoint.Attached);
    }
}
