using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using ZLogger;

namespace DevTools.Daemon.Mcp.Hosting;

/// <summary>Logs MCP Tasks requests (<c>tasks/*</c>) and their responses at the protocol boundary.</summary>
internal sealed class McpTaskLogFilter(ILogger logger)
{
    private const string TasksPrefix = "tasks/";
    private const string TaskIdKey = "taskId";
    private const string StatusKey = "status";

    private readonly ConcurrentDictionary<RequestId, PendingTaskRequest> _pending = new();

    public void Attach(McpServerOptions options)
    {
        options.Filters.Message.IncomingFilters.Add(next => (context, cancellationToken) =>
        {
            OnIncoming(context.JsonRpcMessage);
            return next(context, cancellationToken);
        });
        options.Filters.Message.OutgoingFilters.Add(next => (context, cancellationToken) =>
        {
            OnOutgoing(context.JsonRpcMessage);
            return next(context, cancellationToken);
        });
    }

    internal void OnIncoming(JsonRpcMessage? message)
    {
        if (message is not JsonRpcRequest request || !request.Method.StartsWith(TasksPrefix, StringComparison.Ordinal))
            return;

        var taskId = ReadString(request.Params, TaskIdKey);
        _pending[request.Id] = new PendingTaskRequest(request.Method, taskId);
        logger.ZLogInformation($"{request.Method} request id={request.Id} taskId={taskId}");
    }

    internal void OnOutgoing(JsonRpcMessage? message)
    {
        switch (message)
        {
            case JsonRpcResponse response when _pending.TryRemove(response.Id, out var pending):
                logger.ZLogInformation(
                    $"{pending.Method} ok id={response.Id} taskId={pending.TaskId} status={ReadStatus(response.Result)}");
                break;
            case JsonRpcError error when _pending.TryRemove(error.Id, out var pending):
                logger.ZLogWarning(
                    $"{pending.Method} error id={error.Id} taskId={pending.TaskId} code={error.Error.Code} message={error.Error.Message}");
                break;
        }
    }

    private static string? ReadStatus(JsonNode? result) =>
        ReadString(result, StatusKey) ?? ReadString((result as JsonObject)?["task"], StatusKey);

    private static string? ReadString(JsonNode? node, string key) =>
        (node as JsonObject)?[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private readonly record struct PendingTaskRequest(string Method, string? TaskId);
}
