using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Linq;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using RevitDevTool.Contracts;

namespace RevitDevTool.Server;

public sealed class RevitBridgeClient : IAsyncDisposable
{
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    
    public bool IsConnected => _tcp?.Connected == true && _stream is not null;

    public async Task<bool> ConnectAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            _tcp = new TcpClient();
            await _tcp.ConnectAsync("127.0.0.1", port, cancellationToken).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Cleanup();
            return false;
        }
    }

    public async Task<IReadOnlyList<Tool>> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return [];
        }

        var request = new Envelope
        {
            Kind = BridgeMessageKinds.Request,
            Action = BridgeActions.ListTools
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null || response.IsError)
            return [];

        var toolsWrapper = BridgeFrameCodec.ReadBody<McpToolsListResponseBody>(response);
        return toolsWrapper?.Tools ?? [];
    }

    public async Task<IReadOnlyList<Prompt>> ListPromptsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return [];
        }

        var request = new Envelope
        {
            Kind = BridgeMessageKinds.Request,
            Action = BridgeActions.ListPrompts
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null || response.IsError)
            return [];

        var promptsWrapper = BridgeFrameCodec.ReadBody<McpPromptsListResponseBody>(response);
        return promptsWrapper?.Prompts ?? [];
    }

    public async Task<(IReadOnlyList<Resource> Resources, IReadOnlyList<ResourceTemplate> Templates)> ListResourcesAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return ([], []);
        }

        var request = new Envelope
        {
            Kind = BridgeMessageKinds.Request,
            Action = BridgeActions.ListResources
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null || response.IsError)
            return ([], []);

        var body = BridgeFrameCodec.ReadBody<McpResourcesListResponseBody>(response);
        return (body?.Resources ?? [], body?.ResourceTemplates ?? []);
    }

    public async Task<McpToolExecutionResult> CallToolAsync(
        string toolId,
        string toolName,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
        {
            return McpToolExecutionResult.Failed(BridgeErrorCodes.Disconnected, "Revit bridge is not connected.");
        }

        var request = new Envelope
        {
            Kind = BridgeMessageKinds.Request,
            Action = BridgeActions.ToolCall,
            Body = BridgeFrameCodec.SerializeBody(new McpToolCallRequestBody
            {
                ToolId = toolId,
                ToolName = toolName,
                PayloadJson = payloadJson
            })
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
            return McpToolExecutionResult.Failed(BridgeErrorCodes.Disconnected, "Revit bridge connection lost during tool call.");

        if (response.IsError)
        {
            var errorBody = BridgeFrameCodec.ReadBody<McpErrorBody>(response);
            return McpToolExecutionResult.Failed(
                errorBody?.Code ?? BridgeErrorCodes.ToolFailed,
                errorBody?.Message ?? "Unknown bridge error.",
                errorBody?.Details);
        }

        var responseBody = BridgeFrameCodec.ReadBody<McpToolCallResponseBody>(response);
        if (responseBody is null)
            return McpToolExecutionResult.Failed(BridgeErrorCodes.InvalidResponse, "Bridge returned an empty tool response.");

        if (responseBody.State == ExecutionState.Completed)
            return McpToolExecutionResult.Completed(responseBody.Result, responseBody.Detail);

        return McpToolExecutionResult.Failed(
            responseBody.State == ExecutionState.Cancelled ? BridgeErrorCodes.ToolCancelled : BridgeErrorCodes.ToolFailed,
            responseBody.Detail);
    }

    public async Task<GetPromptResult> GetPromptAsync(
        string promptId,
        string promptName,
        IReadOnlyDictionary<string, JsonElement>? arguments,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Revit bridge is not connected.");

        var request = new Envelope
        {
            Kind = BridgeMessageKinds.Request,
            Action = BridgeActions.GetPrompt,
            Body = BridgeFrameCodec.SerializeBody(new McpPromptGetRequestBody
            {
                PromptId = promptId,
                PromptName = promptName,
                Arguments = arguments,
            })
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
            throw new InvalidOperationException("Revit bridge connection lost during prompt execution.");

        if (response.IsError)
        {
            var errorBody = BridgeFrameCodec.ReadBody<McpErrorBody>(response);
            throw new InvalidOperationException(errorBody?.Message ?? "Unknown bridge error.");
        }

        var responseBody = BridgeFrameCodec.ReadBody<McpPromptGetResponseBody>(response)
                           ?? throw new InvalidOperationException("Bridge returned an empty prompt response.");
        return responseBody.Result;
    }

    public async Task<ReadResourceResult> ReadResourceAsync(
        string resourceId,
        string resourceName,
        string uri,
        CancellationToken cancellationToken = default)
    {
        if (!IsConnected)
            throw new InvalidOperationException("Revit bridge is not connected.");

        var request = new Envelope
        {
            Kind = BridgeMessageKinds.Request,
            Action = BridgeActions.ReadResource,
            Body = BridgeFrameCodec.SerializeBody(new McpResourceReadRequestBody
            {
                ResourceId = resourceId,
                ResourceName = resourceName,
                Uri = uri,
            })
        };

        var response = await SendAndReceiveAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
            throw new InvalidOperationException("Revit bridge connection lost during resource read.");

        if (response.IsError)
        {
            var errorBody = BridgeFrameCodec.ReadBody<McpErrorBody>(response);
            throw new InvalidOperationException(errorBody?.Message ?? "Unknown bridge error.");
        }

        var responseBody = BridgeFrameCodec.ReadBody<McpResourceReadResponseBody>(response)
                           ?? throw new InvalidOperationException("Bridge returned an empty resource response.");
        return responseBody.Result;
    }

    private async Task<Envelope?> SendAndReceiveAsync(Envelope request, CancellationToken cancellationToken)
    {
        if (_stream is null)
        {
            return null;
        }

        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await BridgeFrameCodec.WriteEnvelopeAsync(_stream, request, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }

            while (true)
            {
                var response = await BridgeFrameCodec.ReadEnvelopeAsync(_stream, cancellationToken).ConfigureAwait(false);
                if (response is null)
                {
                    Cleanup();
                    return null;
                }

                if (response.Kind == BridgeMessageKinds.Event)
                    continue;

                if (!string.IsNullOrEmpty(request.Id) && response.Id != request.Id)
                    Trace.TraceWarning($"[MCP] Response ID mismatch: expected '{request.Id}', got '{response.Id}'");

                return response;
            }
        }
        catch
        {
            Cleanup();
            return null;
        }
    }

    private void Cleanup()
    {
        _stream = null;
        try { _tcp?.Close(); } catch { /* ignored */ }
        _tcp = null;
    }

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _tcp?.Dispose();
        return ValueTask.CompletedTask;
    }

}
