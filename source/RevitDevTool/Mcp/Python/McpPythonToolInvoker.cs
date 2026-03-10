using System.IO;
using System.Diagnostics;
using Python.Runtime;
using RevitDevTool.Contracts;
using RevitDevTool.Execution.Providers.Python;
using RevitDevTool.Mcp.Interfaces;
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

        PythonInitializer.InitializeAsync().GetAwaiter().GetResult();
        
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
                scope.Exec(PythonEmbedded.ToolInvokeScript);

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
}
