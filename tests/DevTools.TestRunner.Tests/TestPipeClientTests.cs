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

    public TestContext TestContext { get; set; } = null!;

    private static async Task ReplyAsync(BridgePipeConnection server, BridgeMessage message)
    {
        if (message.Id is null)
            return;

        var other = BridgeMessage.Response(
            "other-id",
            JsonSerializer.SerializeToElement(
                new TestHelloResponse(
                    TestingProtocol.CurrentVersion,
                    TestFrameworkId.NUnit,
                    "Revit",
                    "2026",
                    1,
                    false),
                TestingJsonContext.Default.TestHelloResponse));
        await server.WriteAsync(other).ConfigureAwait(false);
        await server.WriteAsync(
            BridgeMessage.Error(message.Id, TestingProtocol.IncompatibleCode, "wrong session"))
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
