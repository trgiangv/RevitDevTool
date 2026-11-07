using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Transport;
using DevTools.TestRunner.Services;
using System.Text.Json;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class TestPipeClientTests
{
    [TestMethod]
    public async Task HelloAsync_IgnoresAnotherRequestId()
    {
        await using var duplex = new DuplexMemoryStream();
        using var server = new BridgePipeConnection(duplex.Server);
        await using var client = TestPipeClient.ConnectForTesting(duplex.Client);

        server.MessageReceived += message => _ = ReplyAsync(server, message);
        server.StartReadLoop();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.HelloAsync(TestFrameworkId.NUnit, TestContext.CancellationToken));

        Assert.Contains("wrong session", error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task HelloAsync_rejects_a_host_whose_range_is_above_the_runner()
    {
        await using var duplex = new DuplexMemoryStream();
        using var server = new BridgePipeConnection(duplex.Server);
        await using var client = TestPipeClient.ConnectForTesting(duplex.Client);

        var newer = new ProtocolSender("9.0.0", new ProtocolRange(TestingProtocol.CurrentVersion + 1, TestingProtocol.CurrentVersion + 1));
        server.MessageReceived += message => _ = ReplyWithHostSenderAsync(server, message, newer);
        server.StartReadLoop();

        var error = await Assert.ThrowsAsync<HostIncompatibleException>(() =>
            client.HelloAsync(TestFrameworkId.NUnit, TestContext.CancellationToken));

        Assert.Contains("DevTool add-in 9.0.0 is not compatible with DevTools.TestRunner", error.Message, StringComparison.Ordinal);
        Assert.EndsWith(TestingProtocol.UpdateProductMessage, error.Message, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task HelloAsync_tells_the_user_to_restart_a_host_without_a_sender()
    {
        await using var duplex = new DuplexMemoryStream();
        using var server = new BridgePipeConnection(duplex.Server);
        await using var client = TestPipeClient.ConnectForTesting(duplex.Client);

        server.MessageReceived += message => _ = ReplyWithHostSenderAsync(server, message, sender: null);
        server.StartReadLoop();

        var error = await Assert.ThrowsAsync<HostIncompatibleException>(() =>
            client.HelloAsync(TestFrameworkId.NUnit, TestContext.CancellationToken));

        Assert.AreEqual(
            $"DevTool add-in is not compatible with DevTools.TestRunner {TestingProtocol.Sender.Version}. {TestingProtocol.RestartHostMessage}",
            error.Message);
    }

    [TestMethod]
    public async Task RunAsync_surfaces_the_host_sentence_for_a_protocol_incompatible_run()
    {
        const string sentence = "NUnit is older than what the adapter needs.";
        await using var duplex = new DuplexMemoryStream();
        using var server = new BridgePipeConnection(duplex.Server);
        await using var client = TestPipeClient.ConnectForTesting(duplex.Client);

        server.MessageReceived += message =>
            _ = server.WriteAsync(TestingProtocol.CreateIncompatibleResponse(message.Id!, sentence));
        server.StartReadLoop();

        var error = await Assert.ThrowsAsync<HostIncompatibleException>(() =>
            client.RunAsync(
                new TestRunRequest(
                    Guid.NewGuid(),
                    TestFrameworkId.NUnit,
                    new TestAssemblyReference(@"C:\tests\Sample.dll"),
                    TestSelection.All),
                progress: null,
                TestContext.CancellationToken));

        Assert.AreEqual(sentence, error.Message);
    }

    public TestContext TestContext { get; set; } = null!;

    private static async Task ReplyWithHostSenderAsync(BridgePipeConnection server, BridgeMessage message, ProtocolSender? sender)
    {
        if (message.Id is null)
            return;

        await server.WriteAsync(BridgeMessage.Response(
                message.Id,
                JsonSerializer.SerializeToElement(
                    new TestHelloResponse(
                        TestFrameworkId.NUnit,
                        "Revit",
                        "2026",
                        1,
                        false,
                        sender),
                    TestingJsonContext.Default.TestHelloResponse)))
            .ConfigureAwait(false);
    }

    private static async Task ReplyAsync(BridgePipeConnection server, BridgeMessage message)
    {
        if (message.Id is null)
            return;

        var other = BridgeMessage.Response(
            "other-id",
            JsonSerializer.SerializeToElement(
                new TestHelloResponse(TestFrameworkId.NUnit, "Revit", "2026", 1, false, TestingProtocol.Sender),
                TestingJsonContext.Default.TestHelloResponse));
        await server.WriteAsync(other).ConfigureAwait(false);
        await server.WriteAsync(
            BridgeMessage.Error(message.Id, IpcErrorCodes.InternalError, "wrong session"))
            .ConfigureAwait(false);
    }

    private sealed class DuplexMemoryStream : IAsyncDisposable
    {
        private readonly System.IO.Pipelines.Pipe _aToB = new();
        private readonly System.IO.Pipelines.Pipe _bToA = new();

        public Stream Server => new BidirectionalStream(_bToA.Reader.AsStream(), _aToB.Writer.AsStream());
        public Stream Client => new BidirectionalStream(_aToB.Reader.AsStream(), _bToA.Writer.AsStream());

        public ValueTask DisposeAsync()
        {
            _aToB.Writer.Complete();
            _bToA.Writer.Complete();
            return ValueTask.CompletedTask;
        }

        private sealed class BidirectionalStream(Stream reader, Stream writer) : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush() => writer.Flush();
            public override Task FlushAsync(CancellationToken cancellationToken) => writer.FlushAsync(cancellationToken);
            public override int Read(byte[] buffer, int offset, int count) => reader.Read(buffer, offset, count);
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => reader.ReadAsync(buffer, offset, count, cancellationToken);
            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
                => reader.ReadAsync(buffer, cancellationToken);
            public override void Write(byte[] buffer, int offset, int count) => writer.Write(buffer, offset, count);
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => writer.WriteAsync(buffer, offset, count, cancellationToken);
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
                => writer.WriteAsync(buffer, cancellationToken);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
        }
    }
}
