using System.Diagnostics;
using DevTools.Ipc;
using DevTools.Mcp.Client;
using DevTools.Mcp.Client.Tests.Harness;
using DevTools.Mcp.Core.Sessions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol;

namespace DevTools.Mcp.Client.Tests;

public sealed class HostBrokerIntegrationTests
{
    [Fact]
    public async Task RunAsync_ConnectsRefreshesCatalog_AndDisconnectsWhenPipeRemoved()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        await using var broker = new HostBroker(scanner, NullLogger<HostBroker>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);
        var changedCount = 0;
        broker.Changed += () => Interlocked.Increment(ref changedCount);

        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is not null,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        var session = Assert.IsType<HostSession>(broker.GetByProcessId(Environment.ProcessId));
        Assert.NotNull(session);
        Assert.True(session.IsConnected);
        Assert.Equal(host.PipeName, session.PipeName);

        var byKey = broker.GetByHostKey(session.Key);
        Assert.Same(session, byKey);

        var entry = Assert.Single(broker.Catalog.List());
        Assert.Equal("echo", entry.Tools[0].Name);
        Assert.Equal("revit://version", entry.Resources[0].Uri);
        Assert.True(changedCount > 0);

        scanner.SetPipes();
        await WaitUntilAsync(
            () => broker.GetByProcessId(Environment.ProcessId) is null,
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);
        Assert.Empty(broker.Catalog.List());

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [Fact]
    public async Task RunAsync_IgnoresUnreachablePipe()
    {
        var deadPipe = HostPipeName.FormatMcp("Revit", "2025", int.MaxValue);
        var scanner = new FakePipeScanner();
        scanner.SetPipes(deadPipe);
        await using var broker = new HostBroker(scanner, NullLogger<HostBroker>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);

        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Null(broker.GetByProcessId(int.MaxValue));
        Assert.Empty(broker.Catalog.List());

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
    }

    [Fact]
    public async Task DisposeAsync_ClearsSessionsAndCatalog()
    {
        await using var host = await FakeMcpHostPipe.StartAsync(cancellationToken: TestContext.Current.CancellationToken);

        var scanner = new FakePipeScanner();
        scanner.SetPipes(host.PipeName);
        var broker = new HostBroker(scanner, NullLogger<HostBroker>.Instance, NullLoggerFactory.Instance);

        using var runCts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = broker.RunAsync(runCts.Token);

        await WaitUntilAsync(
            () => broker.Catalog.List().Count > 0,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        await runCts.CancelAsync();
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }

        await broker.DisposeAsync();
        Assert.Empty(broker.Catalog.List());
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
