using System.Diagnostics;
using DevTools.Ipc;
using DevTools.Daemon.Mcp.Processes;
using DevTools.Mcp.Client.Tests.Harness;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ProcessSessionsIntegrationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_ConnectsRefreshesCatalog_AndDisconnectsWhenPipeRemoved()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        await using var broker = new ProcessSessions(scanner, NullLogger<ProcessSessions>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);
        var changedCount = 0;
        broker.Changed += () => Interlocked.Increment(ref changedCount);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is not null,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        var session = Assert.IsInstanceOfType<ProcessSession>(broker.GetByProcessId(Environment.ProcessId));
        Assert.IsNotNull(session);
        Assert.IsTrue(session.IsConnected);
        Assert.AreEqual(host.PipeName, session.PipeName);

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
    public async Task RunAsync_ReconnectsWhenTransportDropsButPipeStillDiscovered()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        await using var broker = new ProcessSessions(scanner, NullLogger<ProcessSessions>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is { IsConnected: true },
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        await host.EndCurrentSessionAsync(TestContext.CancellationToken);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is null,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        await WaitUntilAsync(
            () => host.ListenGeneration >= 2,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is { IsConnected: true },
            TimeSpan.FromSeconds(15),
            TestContext.CancellationToken);

        await WaitUntilAsync(
            () => broker.Catalog.List().Count > 0,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [TestMethod]
    public async Task RunAsync_DoesNotReconnectWhenPipeDisappearsFromDiscovery()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        await using var broker = new ProcessSessions(scanner, NullLogger<ProcessSessions>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is { IsConnected: true },
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        scanner.SetPipes();

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is null,
            TimeSpan.FromSeconds(10),
            TestContext.CancellationToken);

        await Task.Delay(2500, TestContext.CancellationToken);

        Assert.IsNull(broker.GetByProcessId(Environment.ProcessId));
        Assert.IsEmpty(broker.Catalog.List());

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [TestMethod]
    public async Task RunAsync_DeadProcessDoesNotBlockALivePipe()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.CancellationToken);
        var deadPipe = HostPipeName.FormatMcp("Revit", "2025", int.MaxValue);
        var scanner = new FakePipeScanner();
        scanner.SetPipes(deadPipe);
        await using var broker = new ProcessSessions(scanner, NullLogger<ProcessSessions>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);
        await Task.Delay(200, TestContext.CancellationToken);
        scanner.SetPipes(deadPipe, host.PipeName);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is { IsConnected: true },
            TimeSpan.FromSeconds(8),
            TestContext.CancellationToken);

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [TestMethod]
    public async Task RunAsync_IgnoresUnreachablePipe()
    {
        var deadPipe = HostPipeName.FormatMcp("Revit", "2025", int.MaxValue);
        var scanner = new FakePipeScanner();
        scanner.SetPipes(deadPipe);
        await using var broker = new ProcessSessions(scanner, NullLogger<ProcessSessions>.Instance, NullLoggerFactory.Instance);

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
        var broker = new ProcessSessions(scanner, NullLogger<ProcessSessions>.Instance, NullLoggerFactory.Instance);

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
