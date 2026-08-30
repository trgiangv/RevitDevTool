using System.Text.Json;

namespace DevTools.Mcp.Tests;

public class PytestBridgeFramingTests
{
    [Fact]
    public async Task BridgePipeConnection_RoundTripsLengthPrefixedFrames()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(
            BridgeMessage.Request("1", "tests/run", JsonSerializer.SerializeToElement(new { workspace_root = "C:\\ws", test_root = "C:\\ws\\tests" })),
            IpcJsonContext.Default.BridgeMessage);
        var frame = new byte[4 + body.Length];
        BitConverter.GetBytes(body.Length).CopyTo(frame, 0);
        body.CopyTo(frame, 4);

        // Length-prefixed BridgeMessage framing must remain distinct from SDK NDJSON.
        Assert.Equal(body.Length, BitConverter.ToInt32(frame, 0));
        Assert.NotEqual((byte)'{', frame[0]);

        await using var duplex = new DuplexMemoryStream();
        using var writer = new BridgePipeConnection(duplex.Server);
        using var reader = new BridgePipeConnection(duplex.Client);

        var tcs = new TaskCompletionSource<BridgeMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.MessageReceived += msg => tcs.TrySetResult(msg);
        reader.StartReadLoop();

        var request = BridgeMessage.Request("42", "instance/info");
        await writer.WriteAsync(request, TestContext.Current.CancellationToken);

        var received = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("42", received.Id);
        Assert.Equal("instance/info", received.Method);
        Assert.Equal(BridgeMessage.TypeRequest, received.Type);
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
