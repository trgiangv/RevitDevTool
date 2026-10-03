using System.IO;
using System.Text.Json;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Hosting;
using DevTools.Mcp.Core.Models;
using DevTools.Mcp.Core.Protocol;
using DevTools.Mcp.Core.Results;
using DevTools.Mcp.Core.Utils;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.Backends;

/// <summary>
/// Owns the complete Python MCP boundary: runtime invocation plus request/result JSON.
/// Protocol encoding is deliberately private to this source.
/// </summary>
public sealed class PythonSource(PythonInitializer initializer) : IMcpSource
{
    private readonly Lock _cacheLock = new();
    private PyObject? _clientCacheState;

    public ExecutionMode SourceKind => ExecutionMode.Python;

    public McpServerTool CreateTool(RegisteredTool tool, IHostContextExecutor hostContext) =>
        SdkCollectionTool.Create(
            tool.Descriptor,
            (request, cancellationToken) => InvokeToolAsync(
                tool,
                request.Params ?? new CallToolRequestParams { Name = tool.Descriptor.Name },
                hostContext,
                cancellationToken));

    public McpServerResource CreateResource(RegisteredResource resource, IHostContextExecutor hostContext) =>
        SdkCollectionResource.Create(resource, hostContext, (uri, cancellationToken) => ReadResourceAsync(resource, uri, cancellationToken));

    public async Task<CallToolResult> InvokeToolAsync(
        RegisteredTool tool,
        CallToolRequestParams request,
        IHostContextExecutor hostContext,
        CancellationToken cancellationToken)
    {
        var sourcePath = RequireSourcePath(tool.Binding.SourcePath);
        var root = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var mtimeTicks = File.GetLastWriteTimeUtc(sourcePath).Ticks;
        EnsureToolsetServer(sourcePath, root, mtimeTicks);

        return await hostContext.ExecuteAsync(
            () =>
            {
                var resultJson = RunToolInvoke(
                    sourcePath,
                    root,
                    mtimeTicks,
                    scope =>
                    {
                        scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationTool));
                        scope.Set(PythonInstances.ToolName, new PyString(tool.Descriptor.Name));
                        scope.Set(PythonInstances.PayloadJson, new PyString(WriteRequest(request)));
                    });
                return ReadToolResult(resultJson);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task<ReadResourceResult> ReadResourceAsync(
        RegisteredResource resource,
        string uri,
        CancellationToken cancellationToken)
    {
        var sourcePath = RequireSourcePath(resource.Binding.SourcePath);
        var root = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var mtimeTicks = File.GetLastWriteTimeUtc(sourcePath).Ticks;
        EnsureToolsetServer(sourcePath, root, mtimeTicks);

        var resultJson = RunToolInvoke(
            sourcePath,
            root,
            mtimeTicks,
            scope =>
            {
                scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationResource));
                scope.Set(PythonInstances.ResourceName, new PyString(resource.DisplayName));
                scope.Set(PythonInstances.ResourceUri, new PyString(uri));
            });
        return Task.FromResult(ReadResourceResult(resultJson));
    }

    public void ClearCaches()
    {
        using (Py.GIL())
        {
            lock (_cacheLock)
            {
                if (_clientCacheState is null)
                    return;

                try
                {
                    RunToolInvokeOnCache(
                        _clientCacheState,
                        static scope => scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationClearCache)));
                }
                finally
                {
                    _clientCacheState.Dispose();
                    _clientCacheState = null;
                }
            }
        }
    }

    private void EnsureToolsetServer(string sourcePath, string root, long mtimeTicks)
    {
        RunToolInvoke(
            sourcePath,
            root,
            mtimeTicks,
            static scope => scope.Set(PythonInstances.Operation, new PyString(PythonInstances.OperationEnsureServer)));
    }

    private string RunToolInvoke(string sourcePath, string root, long mtimeTicks, Action<PyModule> configureScope)
    {
        using (Py.GIL())
        {
            PyObject cache;
            lock (_cacheLock)
            {
                cache = GetOrCreateCacheState();
            }

            return PythonExecutor.Execute(
                initializer,
                sourcePath,
                root,
                scope =>
                {
                    AttachInvokeScope(scope, sourcePath, root, mtimeTicks, cache);
                    configureScope(scope);
                    scope.Exec(PythonEmbedded.ToolInvokeScript);
                    return scope.Get(PythonInstances.ResultJson).As<string>();
                });
        }
    }

    private static void RunToolInvokeOnCache(PyObject cache, Action<PyModule> configureScope)
    {
        using var scope = Py.CreateScope();
        scope.Set(PythonInstances.McpClientCache, cache);
        configureScope(scope);
        scope.Exec(PythonEmbedded.ToolInvokeScript);
    }

    private PyObject GetOrCreateCacheState()
    {
        if (_clientCacheState is not null)
            return _clientCacheState;

        _clientCacheState = new PyDict();
        return _clientCacheState;
    }

    private static void AttachInvokeScope(
        PyModule scope,
        string sourcePath,
        string root,
        long mtimeTicks,
        PyObject cache)
    {
        scope.Set(PythonInstances.McpClientCache, cache);
        scope.Set(PythonInstances.SourceMtimeUtcTicks, new PyInt(mtimeTicks));
        scope.Set(PythonInstances.SourceFile, new PyString(sourcePath));
    }

    internal static string WriteRequest(CallToolRequestParams? request)
    {
        if (request is null)
            return "{}";

        var payload = new Dictionary<string, object?>();
        if (request.Arguments is { Count: > 0 } arguments)
            payload[McpSpecKeys.Tools.Arguments] = arguments;
        return JsonSerializer.Serialize(payload, ToolHelpers.ProtocolOptions);
    }

    internal static CallToolResult ReadToolResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (IsInputRequired(document.RootElement))
            return ToolHelpers.ErrorResult("Input required is not supported on the host.");
        return Deserialize<CallToolResult>(document.RootElement, "tool");
    }

    internal static ReadResourceResult ReadResourceResult(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (IsInputRequired(document.RootElement))
            throw new InvalidOperationException("Input required is not supported on the host.");
        var result = Deserialize<ReadResourceResult>(document.RootElement, "resource");
        if (result.Contents.Any(static item => item is not TextResourceContents and not BlobResourceContents))
            throw new InvalidOperationException("Python MCP resource contents must be text or blob entries.");
        return result;
    }

    private static T Deserialize<T>(JsonElement root, string kind)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(root.GetRawText(), ToolHelpers.ProtocolOptions)
                   ?? throw new InvalidOperationException($"Python MCP {kind} result was null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Python MCP {kind} result was malformed.", ex);
        }
    }

    private static bool IsInputRequired(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object &&
        root.TryGetProperty(McpSpecKeys.ResultType.Key, out var resultType) &&
        resultType.GetString() == McpSpecKeys.ResultType.InputRequired;

    private static string RequireSourcePath(string? sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");
        return sourcePath!;
    }
}
