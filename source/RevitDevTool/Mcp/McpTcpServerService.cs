using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp;

public sealed class McpTcpServerService(
    McpRegistryService registryService,
    McpExecutionQueue executionQueue, 
    Models.McpBridgeState state, 
    McpToolExecutionDispatcher dispatcher) : IHostedService, IDisposable
{
    private const int DefaultPort = 18080;
    private static readonly JsonSerializerOptions BridgeJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private TcpListener? _listener;
    private CancellationTokenSource? _serverCts;
    private Task? _acceptLoopTask;

    private int _connectedClients;
    private int _port;
    private bool _disposed;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_listener is not null)
            return Task.CompletedTask;

        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        _port = FindAvailablePort(DefaultPort);

        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        
        state.SetEndpoint(_port.ToString());
        Trace.TraceInformation($"[MCP/TCP] Listening on {_port}");

        _serverCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _acceptLoopTask = RunAcceptLoopAsync(_serverCts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
            return;

        Interlocked.Exchange(ref _connectedClients, 0);
        state.SetConnectedState(0);
        state.SetQueueDepth(0);

        _serverCts?.Cancel();
        _listener.Stop();

        if (_acceptLoopTask is not null)
        {
            try
            {
#if NET
                await _acceptLoopTask.WaitAsync(cancellationToken).ConfigureAwait(false);
#else
                await _acceptLoopTask.ConfigureAwait(false);
#endif
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"[MCP/TCP] Accept loop shutdown warning: {ex.Message}");
            }
        }

        _serverCts?.Dispose();
        _serverCts = null;
        _acceptLoopTask = null;
        _listener = null;
    }

    private async Task RunAcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var acceptedClient = client;
                var count = Interlocked.Increment(ref _connectedClients);
                state.SetConnectedState(count);
                Trace.TraceInformation($"[MCP/TCP] Client connected. Active clients: {count}");
                _ = HandleClientAsync(acceptedClient, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                Trace.TraceError($"[MCP/TCP] Accept error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = client.GetStream();
            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var request = await ReadEnvelopeAsync(stream, cancellationToken).ConfigureAwait(false);
                if (request is null)
                    break;

                var response = await HandleRequestAsync(request, cancellationToken).ConfigureAwait(false);
                await WriteEnvelopeAsync(stream, response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (SocketException)
        {
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[MCP/TCP] Client handler error: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            try
            {
                client.Close();
            }
            catch
            {
                // ignored
            }

            var count = Math.Max(0, Interlocked.Decrement(ref _connectedClients));
            state.SetConnectedState(count);
            Trace.TraceInformation($"[MCP/TCP] Client disconnected. Active clients: {count}");
        }
    }

    private static async Task<Envelope?> ReadEnvelopeAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = await ReadExactAsync(stream, 4, cancellationToken).ConfigureAwait(false);
        if (header is null)
            return null;

        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (payloadLength <= 0 || payloadLength > 4 * 1024 * 1024)
            throw new InvalidDataException($"Invalid payload length: {payloadLength}");

        var payloadBytes = await ReadExactAsync(stream, payloadLength, cancellationToken).ConfigureAwait(false);
        if (payloadBytes is null)
            return null;

        var payloadJson = Encoding.UTF8.GetString(payloadBytes);
        return JsonSerializer.Deserialize<Envelope>(payloadJson, BridgeJsonOptions);
    }

    private static async Task<byte[]?> ReadExactAsync(NetworkStream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
#if NET
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
#else
            var read = await stream.ReadAsync(buffer, offset, length - offset, cancellationToken).ConfigureAwait(false);
#endif
            if (read == 0)
                return offset == 0 ? null : throw new EndOfStreamException("Socket closed mid-frame.");
            offset += read;
        }

        return buffer;
    }

    private static async Task WriteEnvelopeAsync(NetworkStream stream, Envelope envelope, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(envelope, BridgeJsonOptions);
        var payload = Encoding.UTF8.GetBytes(json);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
#if NET
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
#else
        await stream.WriteAsync(header, 0, header.Length, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
#endif
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<Envelope> HandleRequestAsync(Envelope message, CancellationToken cancellationToken)
    {
        if (!string.Equals(message.SchemaVersion, McpProtocol.SchemaVersion, StringComparison.Ordinal) ||
            !string.Equals(message.SchemaChecksum, McpProtocol.SchemaChecksum, StringComparison.OrdinalIgnoreCase))
        {
            return BuildError(
                message,
                "bridge.schema_mismatch",
                $"Schema mismatch. expected={McpProtocol.SchemaVersion}/{McpProtocol.SchemaChecksum}, got={message.SchemaVersion}/{message.SchemaChecksum}");
        }

        return message.Action switch
        {
            McpActions.Ping => BuildOk(message, McpActions.Pong, JsonSerializer.Serialize(new
            {
                protocol = McpProtocol.Version,
                schemaVersion = McpProtocol.SchemaVersion,
                schemaChecksum = McpProtocol.SchemaChecksum,
                endpoint = state.Endpoint,
                port = _port
            }, BridgeJsonOptions)),
            McpActions.ListTools => HandleListTools(message),
            McpActions.ToolCall => await HandleToolCallAsync(message, cancellationToken).ConfigureAwait(false),
            McpActions.GetExecution => HandleGetExecution(message),
            McpActions.CancelExecution => HandleCancelExecution(message),
            McpActions.Shutdown => HandleShutdown(message),
            _ => BuildError(message, "bridge.unknown_action", $"Unknown action '{message.Action}'")
        };
    }

    private Envelope HandleListTools(Envelope message)
    {
        var tools = registryService.EnsureToolsLoaded();
        Trace.TraceInformation($"[MCP/TCP] tools.list returning {tools.Count} tool(s) on port {_port}.");

        return BuildOk(message, McpActions.ListTools, JsonSerializer.Serialize(new
        {
            tools
        }, BridgeJsonOptions));
    }

    private Envelope HandleShutdown(Envelope message)
    {
        state.SetQueueDepth(0);
        return BuildOk(message, McpActions.Shutdown, "{\"shutdown\":\"detached\"}");
    }

    private async Task<Envelope> HandleToolCallAsync(Envelope message, CancellationToken cancellationToken)
    {
        // ReSharper disable once RedundantSuppressNullableWarningExpression
        var requestId = string.IsNullOrWhiteSpace(message.Id) ? "<no-id>" : message.Id!;
        if (string.IsNullOrWhiteSpace(message.ToolId) && string.IsNullOrWhiteSpace(message.ToolName))
            return BuildError(message, "tool.missing_name", "ToolId or ToolName is required.");

        registryService.EnsureToolsLoaded();

        var requestedTool = !string.IsNullOrWhiteSpace(message.ToolId) ? message.ToolId! : message.ToolName!;
        Trace.TraceInformation($"[MCP/TCP] Tool call received. requestId={requestId}, tool={requestedTool}");
        if (!registryService.TryGetTool(message.ToolId, message.ToolName, out var definition) || definition is null)
            return BuildError(message, "tool.not_found", $"Tool '{requestedTool}' is not registered.");

        var executionId = string.IsNullOrWhiteSpace(message.ExecutionId)
            ? Guid.NewGuid().ToString("N")
            : message.ExecutionId;

        var result = await executionQueue.EnqueueAsync(
            executionId,
            definition.ToolId,
            definition.Name,
            (progress, token) => ExecuteOnRevitThreadAsync(message, definition, progress, token),
            cancellationToken).ConfigureAwait(false);

        if (result.Success)
        {
            state.RecordCall(definition.ToolId, definition.Name);
            Trace.TraceInformation($"[MCP/TCP] Tool call succeeded. requestId={requestId}, tool={definition.ToolId}");
            return BuildOk(message, McpActions.ToolCall, result, executionId, definition);
        }

        Trace.TraceWarning(
            $"[MCP/TCP] Tool call failed. requestId={requestId}, tool={definition.ToolId}, code={result.Error?.Code}, message={result.Error?.Message}");
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = executionId,
            Kind = McpMessageKinds.Response,
            Action = McpActions.ToolCall,
            ToolId = definition.ToolId,
            ToolName = definition.Name,
            Version = McpProtocol.Version,
            Message = result.Message,
            ResultKind = result.ResultKind,
            Metadata = result.Metadata,
            ProgressUpdates = result.ProgressUpdates.ToList(),
            Error = result.Error
        };
    }

    private Envelope HandleGetExecution(Envelope message)
    {
        if (string.IsNullOrWhiteSpace(message.ExecutionId))
            return BuildError(message, "execution.missing_id", "ExecutionId is required.");

        var snapshot = executionQueue.GetExecutionSnapshot(message.ExecutionId);
        if (snapshot is null)
            return BuildError(message, "execution.not_found", $"Execution '{message.ExecutionId}' was not found.");

        return BuildExecutionEnvelope(message, McpActions.GetExecution, snapshot);
    }

    private Envelope HandleCancelExecution(Envelope message)
    {
        if (string.IsNullOrWhiteSpace(message.ExecutionId))
            return BuildError(message, "execution.missing_id", "ExecutionId is required.");

        if (!executionQueue.TryCancel(message.ExecutionId))
            return BuildError(message, "execution.not_found", $"Execution '{message.ExecutionId}' was not found or cannot be cancelled.");

        var snapshot = executionQueue.GetExecutionSnapshot(message.ExecutionId);
        if (snapshot is null)
            return BuildError(message, "execution.not_found", $"Execution '{message.ExecutionId}' was not found.");

        return BuildExecutionEnvelope(message, McpActions.CancelExecution, snapshot);
    }

    private Task<McpToolExecutionResult> ExecuteOnRevitThreadAsync(
        Envelope message,
        McpToolDefinition definition,
        IProgress<McpProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        return dispatcher.DispatchAsync(definition, message.PayloadJson, progress, cancellationToken);
    }

    private static Envelope BuildOk(
        Envelope message,
        string action,
        McpToolExecutionResult result,
        string executionId,
        McpToolDefinition definition)
    {
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = executionId,
            Kind = McpMessageKinds.Response,
            Action = action,
            ToolId = definition.ToolId,
            ToolName = definition.Name,
            Version = McpProtocol.Version,
            PayloadJson = result.PayloadJson,
            Message = result.Message,
            ResultKind = result.ResultKind,
            Metadata = result.Metadata,
            ProgressUpdates = result.ProgressUpdates.ToList()
        };
    }

    private static Envelope BuildOk(Envelope message, string action, string payloadJson)
    {
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = message.ExecutionId,
            Kind = McpMessageKinds.Response,
            Action = action,
            ToolId = message.ToolId,
            ToolName = message.ToolName,
            Version = McpProtocol.Version,
            PayloadJson = payloadJson,
            ResultKind = McpResultKinds.Json
        };
    }

    private static Envelope BuildError(Envelope message, string code, string reason)
    {
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = message.ExecutionId,
            Kind = McpMessageKinds.Response,
            Action = message.Action,
            ToolId = message.ToolId,
            ToolName = message.ToolName,
            Version = McpProtocol.Version,
            Error = new McpException
            {
                Code = code,
                Message = reason
            }
        };
    }

    private static Envelope BuildExecutionEnvelope(Envelope message, string action, McpExecutionSnapshot snapshot)
    {
        return new Envelope
        {
            Id = message.Id,
            ExecutionId = snapshot.ExecutionId,
            Kind = McpMessageKinds.Response,
            Action = action,
            ToolId = snapshot.ToolId,
            ToolName = snapshot.ToolName,
            Version = McpProtocol.Version,
            Message = snapshot.Message,
            ResultKind = snapshot.ResultKind,
            Execution = snapshot,
            ProgressUpdates = snapshot.ProgressUpdates.ToList(),
            Error = snapshot.Error
        };
    }

    private static int FindAvailablePort(int preferredPort)
    {
        try
        {
            var tester = new TcpListener(IPAddress.Loopback, preferredPort);
            tester.Start();
            tester.Stop();
            return preferredPort;
        }
        catch (SocketException)
        {
            var fallback = new TcpListener(IPAddress.Loopback, 0);
            fallback.Start();
            var port = ((IPEndPoint)fallback.LocalEndpoint).Port;
            fallback.Stop();
            return port;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _listener?.Stop();
        _serverCts?.Cancel();
        _serverCts?.Dispose();
    }
}
