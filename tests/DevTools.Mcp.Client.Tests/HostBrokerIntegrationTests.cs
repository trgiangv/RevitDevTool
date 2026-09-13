using System.Diagnostics;
using DevTools.Ipc;
using DevTools.Mcp.Client;
using DevTools.Mcp.Client.Tests.Harness;
using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class HostBrokerIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_ConnectsRefreshesCatalog_AndDisconnectsWhenPipeRemoved()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        await using var broker = new HostBroker(scanner, NullLogger<HostBroker>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);
        var changedCount = 0;
        broker.Changed += () => Interlocked.Increment(ref changedCount);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is not null,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        var session = Assert.IsInstanceOfType<HostSession>(broker.GetByProcessId(Environment.ProcessId));
        Assert.IsNotNull(session);
        Assert.IsTrue(session.IsConnected);
        Assert.AreEqual(host.PipeName, session.PipeName);

        var byKey = broker.GetByHostKey(session.Key);
        Assert.AreSame(session, byKey);

        await WaitUntilAsync(
            () => broker.Catalog.List().Count > 0,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        var entry = broker.Catalog.List().Single();
        Assert.AreEqual("echo", entry.Tools[0].Name);
        Assert.AreEqual("revit://version", entry.Resources[0].Uri);
        Assert.IsTrue(changedCount > 0);

        scanner.SetPipes();
        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is null,
            TimeSpan.FromSeconds(5),
            TestContext.CancellationToken);
        Assert.IsEmpty(broker.Catalog.List());

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [TestMethod]
    public async Task RunAsync_IgnoresUnreachablePipe()
    {
        var deadPipe = HostPipeName.FormatMcp("Revit", "2025", int.MaxValue);
        var scanner = new FakePipeScanner();
        scanner.SetPipes(deadPipe);
        await using var broker = new HostBroker(scanner, NullLogger<HostBroker>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);

        await Task.Delay(500, TestContext.CancellationToken);

        Assert.IsNull(broker.GetByProcessId(int.MaxValue));
        Assert.IsEmpty(broker.Catalog.List());

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [TestMethod]
    public async Task DisposeAsync_ClearsSessionsAndCatalog()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        var broker = new HostBroker(scanner, NullLogger<HostBroker>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);

        await WaitUntilAsync(
            () => broker.Catalog.List().Count > 0,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }

        await broker.DisposeAsync();
        Assert.IsEmpty(broker.Catalog.List());
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (!predicate())
        {
            if (sw.Elapsed >= timeout)
                Assert.Fail("Timed out waiting for condition.");

            await Task.Delay(50, cancellationToken);
        }
    }
}
