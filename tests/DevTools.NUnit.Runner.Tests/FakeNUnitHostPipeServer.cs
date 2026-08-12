using System.Text.Json;
using DevTools.Ipc;
using DevTools.NUnit.Core.Compatibility;
using DevTools.NUnit.Core.Contracts;
using DevTools.NUnit.Core.Results;
using DevTools.NUnit.Runner.Services;

namespace DevTools.NUnit.Runner.Tests;

internal sealed class FakeNUnitHostPipeServer : IAsyncDisposable
{
    private readonly BridgePipeConnection _connection;
    private readonly DuplexMemoryStream _duplex;
    private readonly CancellationTokenSource _cts = new();
    private readonly List<Func<BridgeMessage, CancellationToken, Task>> _handlers = [];
    private readonly TaskCompletionSource _connected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private FakeNUnitHostPipeServer(BridgePipeConnection connection, DuplexMemoryStream duplex)
    {
        _connection = connection;
        _duplex = duplex;
        _connection.MessageReceived += message => _ = HandleAsync(message);
        _connection.StartReadLoop();
    }

    public static async Task<(FakeNUnitHostPipeServer Server, NUnitPipeClient Client)> CreateConnectedPairAsync()
    {
        var duplex = new DuplexMemoryStream();
        var server = new FakeNUnitHostPipeServer(new BridgePipeConnection(duplex.Server), duplex);
        var client = NUnitPipeClient.ConnectForTesting(duplex.Client);
        server._connected.TrySetResult();
        await server._connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
        return (server, client);
    }

    public void OnRequest(Func<BridgeMessage, CancellationToken, Task> handler) => _handlers.Add(handler);

    public void ResetRequestHandlers() => _handlers.Clear();

    public IReadOnlyList<Guid> ObservedCancelRunIds => _observedCancelRunIds;

    private readonly List<Guid> _observedCancelRunIds = [];

    public Task SendNotificationAsync(string method, JsonElement? parameters, CancellationToken ct = default) =>
        _connection.WriteAsync(BridgeMessage.Notification(method, parameters), ct);

    public Task SendResponseAsync(string requestId, JsonElement? result, CancellationToken ct = default) =>
        _connection.WriteAsync(BridgeMessage.Response(requestId, result), ct);

    public Task SendErrorAsync(
        string requestId,
        string code,
        string message,
        JsonElement? data = null,
        CancellationToken ct = default) =>
        _connection.WriteAsync(BridgeMessage.Error(requestId, code, message, data), ct);

    private async Task HandleAsync(BridgeMessage message)
    {
        if (message.Type != BridgeMessage.TypeRequest || message.Method is null)
            return;

        if (message.Method == NUnitProtocol.Cancel)
        {
            var cancel = message.Params?.Deserialize(NUnitJsonContext.Default.NUnitCancelRequest);
            if (cancel is not null)
                lock (_observedCancelRunIds)
                    _observedCancelRunIds.Add(cancel.RunId);
        }

        foreach (var handler in _handlers)
        {
            await handler(message, _cts.Token).ConfigureAwait(false);
            return;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _connection.Dispose();
        await _duplex.DisposeAsync();
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

internal static class FakeNUnitHostResponses
{
    public static NUnitDiscoverResponse SampleDiscoverResponse() =>
        new(
            Cases:
            [
                new NUnitDiscoveredTest(
                    Id: "case-1",
                    Name: "PlainTest",
                    FullName: "Sample.Fixture.PlainTest",
                    ParentTestId: "fixture-1",
                    Traits: [new NUnitTrait("Category", "Smoke")],
                    Source: new NUnitSourceLocation("Fixture.cs", 42),
                    SkipReason: null),
                new NUnitDiscoveredTest(
                    Id: "case-2",
                    Name: "IgnoredTest",
                    FullName: "Sample.Fixture.IgnoredTest",
                    SkipReason: "not ready"),
            ],
            GenerationId: "generation-one",
            RuntimeDiagnostic: new NUnitRuntimeDiagnostic("generation.loaded", "Generation loaded."));

    public static NUnitRunResponse SampleRunResponse(Guid runId) =>
        new(
            RunId: runId,
            Summary: new NUnitRunSummary(1, 0, 0, 0, 0, 0),
            Cases:
            [
                new NUnitCaseResult(
                    Id: "case-1",
                    Name: "PlainTest",
                    Outcome: NUnitOutcomes.Passed,
                    DurationMs: 12.5,
                    Message: null,
                    StackTrace: null,
                    Output: "stdout",
                    ParentTestId: "fixture-1",
                    Traits: [new NUnitTrait("Category", "Smoke")],
                    Source: new NUnitSourceLocation("Fixture.cs", 42),
                    Attachments:
                    [
                        new NUnitAttachment("trace", "text/plain", @"C:\temp\trace.txt", null),
                    ]),
            ],
            GenerationId: "generation-one",
            RuntimeDiagnostic: new NUnitRuntimeDiagnostic("run.complete", "Run complete."));
}
