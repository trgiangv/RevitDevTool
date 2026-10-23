using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipes;

namespace DevTools.Mcp.Client.Tests;

[TestClass]
public sealed class ProcessSessionConnectTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task ConnectAsync_DisposesPipe_WhenCreateAsyncFails()
    {
        var pipeName = HostPipeName.FormatMcp("Revit", "2025", Environment.ProcessId);
        await using var server = new NamedPipeServerStream(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        var listenTask = server.WaitForConnectionAsync(TestContext.CancellationToken);
        var connectTask = ProcessSession.ConnectAsync(
            pipeName,
            NullLoggerFactory.Instance,
            NullLogger.Instance,
            TestContext.CancellationToken);

        await listenTask;
        await server.DisposeAsync();

        await Assert.ThrowsExactlyAsync<IOException>(async () => await connectTask);
    }
}
