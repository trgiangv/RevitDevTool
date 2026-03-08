using System.IO;
using System.Diagnostics;
using Python.Runtime;
using RevitDevTool.Execution.Models;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Mcp.Interfaces;
using RevitDevTool.Mcp.Schemas;
namespace RevitDevTool.Mcp.Python;

public sealed class McpPythonToolInvoker : McpToolInvokerBase
{
    public override bool CanHandle(ExecutionMode executionMode)
    {
        return executionMode == ExecutionMode.Python;
    }

    protected override Task<McpToolExecutionResult> ExecuteCoreAsync(
        McpToolDefinition definition,
        string normalizedPayload,
        IProgress<McpProgressUpdate>? progress,
        CancellationToken cancellationToken)
    {
        var startedAtUtc = DateTime.UtcNow;
        var stopwatch = Stopwatch.StartNew();
        progress?.Report(new McpProgressUpdate
        {
            Stage = "Initializing",
            Message = $"Preparing Python MCP tool '{definition.Name}'..."
        });

        PythonBootstrap.EnsureExecutorReadyAsync(cancellationToken).GetAwaiter().GetResult();
        
        if (string.IsNullOrWhiteSpace(definition.SourcePath) || !File.Exists(definition.SourcePath))
        {
            stopwatch.Stop();
            return Task.FromResult(McpToolExecutionResult.Failed(
                "tool.source_not_found",
                $"Python MCP source file was not found for '{definition.Name}'.",
                $"sourcePath={definition.SourcePath}",
                BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new McpProgressUpdate
            {
                Stage = "Executing",
                Message = $"Executing Python MCP tool '{definition.Name}'..."
            });

            using (Py.GIL())
            {
                if (PythonInitializer.GlobalScope is null)
                {
                    stopwatch.Stop();
                    return Task.FromResult(McpToolExecutionResult.Failed("tool.python_runtime_unavailable",
                        "Global Python scope not initialized.",
                        metadata: BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
                }

                using var scope = PythonInitializer.GlobalScope.NewScope();
                PythonExecutor.PrepareExecutionScope(scope, definition.SourcePath);
                scope.Set("__tool_name__", new PyString(definition.Name));
                scope.Set("__payload_json__", new PyString(normalizedPayload));
                scope.Exec(PythonToolInvocationScript);

                var resultJson = scope.Get("__result_json__").As<string>();
                stopwatch.Stop();
                return Task.FromResult(McpToolExecutionResult.Succeeded(
                    resultJson,
                    $"Completed '{definition.Name}'.",
                    DetermineResultKind(resultJson),
                    BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds),
                    progressUpdates:
                    [
                        new McpProgressUpdate
                        {
                            Stage = "Completed",
                            Message = $"Completed Python MCP tool '{definition.Name}'.",
                        }
                    ]));
            }
        }
        catch (PythonException ex)
        {
            stopwatch.Stop();
            return Task.FromResult(McpToolExecutionResult.Failed(
                "tool.python_invoke_failed",
                ex.Message,
                ex.StackTrace,
                BuildMetadata(definition, startedAtUtc, stopwatch.ElapsedMilliseconds)));
        }
    }

    private static string DetermineResultKind(string resultJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(resultJson);
            return document.RootElement.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Null => McpResultKinds.Empty,
                System.Text.Json.JsonValueKind.String => McpResultKinds.Text,
                _ => McpResultKinds.Json
            };
        }
        catch
        {
            return McpResultKinds.Text;
        }
    }

    private static McpToolExecutionMetadata BuildMetadata(McpToolDefinition definition, DateTime startedAtUtc, long durationMs)
    {
        return new McpToolExecutionMetadata
        {
            ToolId = definition.ToolId,
            ToolName = definition.Name,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = startedAtUtc.AddMilliseconds(durationMs),
            DurationMs = durationMs
        };
    }

    private const string PythonToolInvocationScript = """
        import asyncio
        import importlib.util
        import inspect
        import json
        import uuid

        def __load_module_from_file(file_path):
            module_name = f"rdt_mcp_{uuid.uuid4().hex}"
            spec = importlib.util.spec_from_file_location(module_name, file_path)
            if spec is None or spec.loader is None:
                raise RuntimeError(f"Unable to load Python MCP module: {file_path}")
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            return module

        def __looks_like_mcp_server(obj):
            return obj is not None and hasattr(obj, "call_tool") and callable(getattr(obj, "call_tool"))

        def __resolve_mcp_server(module):
            get_mcp_server = getattr(module, "get_mcp_server", None)
            if callable(get_mcp_server):
                candidate = get_mcp_server()
                if __looks_like_mcp_server(candidate):
                    return candidate

            module_level = getattr(module, "mcp", None)
            if __looks_like_mcp_server(module_level):
                return module_level

            for _name, obj in inspect.getmembers(module):
                if __looks_like_mcp_server(obj):
                    return obj

            raise RuntimeError("No MCP server object (mcp/get_mcp_server) was found in the Python tool module.")

        def __normalize(value):
            if value is None or isinstance(value, (str, int, float, bool)):
                return value
            if isinstance(value, bytes):
                return value.decode("utf-8", errors="replace")
            if hasattr(value, "model_dump"):
                return __normalize(value.model_dump(by_alias=True, exclude_none=True))
            if hasattr(value, "dict") and callable(getattr(value, "dict")):
                return __normalize(value.dict())
            if isinstance(value, dict):
                return {str(key): __normalize(item) for key, item in value.items()}
            if isinstance(value, (list, tuple, set)):
                return [__normalize(item) for item in value]

            for attr_name in ("text", "content", "data", "result"):
                attr_value = getattr(value, attr_name, None)
                if attr_value is not None:
                    return __normalize(attr_value)

            return str(value)

        __payload = json.loads(__payload_json__) if __payload_json__ else {}
        if not isinstance(__payload, dict):
            raise RuntimeError("Tool payload must be a JSON object.")

        __module = __load_module_from_file(__file__)
        __server = __resolve_mcp_server(__module)
        __tool = __server._tool_manager.get_tool(__tool_name__)
        if __tool is None:
            raise RuntimeError(f"MCP tool '{__tool_name__}' was not found.")
        __call_result = asyncio.run(__tool.run(__payload, context=None, convert_result=False))
        __result_json__ = json.dumps(__normalize(__call_result))
        """;
}
