using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ZLogger;

namespace DevTools.Mcp.Server.Hosting;

/// <summary>SDK request filters that log <c>tools/call</c> and <c>resources/read</c> at the protocol boundary.</summary>
internal static class McpLogFilters
{
    public static void Attach(McpServerOptions options, ILoggerFactory loggerFactory)
    {
        options.Filters.Request.CallToolFilters.Add(
            CreateCallToolFilter(loggerFactory.CreateLogger("DevTools.Mcp.ToolCall")));
        options.Filters.Request.ReadResourceFilters.Add(
            CreateReadResourceFilter(loggerFactory.CreateLogger("DevTools.Mcp.ResourceRead")));
    }

    private static McpRequestFilter<CallToolRequestParams, CallToolResult> CreateCallToolFilter(ILogger logger) =>
        next => (request, cancellationToken) => LogCallToolAsync(next, request, logger, cancellationToken);

    private static McpRequestFilter<ReadResourceRequestParams, ReadResourceResult> CreateReadResourceFilter(ILogger logger) =>
        next => (request, cancellationToken) => LogReadResourceAsync(next, request, logger, cancellationToken);

    private static async ValueTask<CallToolResult> LogCallToolAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var target = request.Params.Name;
        var argsJson = McpLogPayload.SerializeArgs(request.Params.Arguments);
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            LogCallToolSuccess(logger, target, sw.ElapsedMilliseconds, argsJson, result);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            logger.ZLogWarning($"tools/call timeout target={target} durationMs={sw.ElapsedMilliseconds} args={argsJson}");
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.ZLogError(ex, $"tools/call error target={target} durationMs={sw.ElapsedMilliseconds} args={argsJson} error={ex.Message}");
            throw;
        }
    }

    private static void LogCallToolSuccess(
        ILogger logger,
        string target,
        long durationMs,
        string argsJson,
        CallToolResult result)
    {
        var outcome = result.IsError == true ? "error" : "ok";
        var resultJson = McpLogPayload.SerializeCallToolResult(result);
        var msg = $"tools/call {outcome} target={target} durationMs={durationMs} args={argsJson} result={resultJson}";
        if (result.IsError == true)
            logger.ZLogWarning($"{msg}");
        else
            logger.ZLogInformation($"{msg}");
    }

    private static async ValueTask<ReadResourceResult> LogReadResourceAsync(
        McpRequestHandler<ReadResourceRequestParams, ReadResourceResult> next,
        RequestContext<ReadResourceRequestParams> request,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var target = request.Params.Uri;
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await next(request, cancellationToken).ConfigureAwait(false);
            sw.Stop();
            var resultJson = McpLogPayload.SerializeReadResourceResult(result);
            var msg = $"resources/read ok target={target} durationMs={sw.ElapsedMilliseconds} result={resultJson}";
            logger.ZLogInformation($"{msg}");
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            logger.ZLogWarning($"resources/read timeout target={target} durationMs={sw.ElapsedMilliseconds}");
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.ZLogError(ex, $"resources/read error target={target} durationMs={sw.ElapsedMilliseconds} error={ex.Message}");
            throw;
        }
    }
}
