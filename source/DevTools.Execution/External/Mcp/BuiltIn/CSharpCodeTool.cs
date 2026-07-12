using System.Runtime.CompilerServices;
using System.Text.Json;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Compiles and executes C# code in the host process via Roslyn.</summary>
public sealed class CSharpCodeTool(
    ICompiledScriptBridge scriptBridge,
    CSharpCompiler compiler,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner) : IBuiltInMcpTool
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    public string Name => "execute_csharp_code";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "execute_csharp_code",
        Description =
            "Compile and execute C# code in the running host process. " +
            "Host API assemblies auto-referenced. Use #r for extras, #r \"nuget:\" for packages.\n" +
            "BEFORE WRITING CODE: Read available resources (list_dynamic_resources) for API patterns and model state.\n" +
            "Error responses: [COMPILATION ERROR] fix code, [RUNTIME ERROR] check logic, [ROLLBACK] constraint violation.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(
                IpcPropertyNames.Code,
                "Complete C# source. Revit: implement IExternalCommand, set 'message' ref param. " +
                "AutoCAD: use [CommandMethod]. Include all usings and attributes.")
        ],
        required: [IpcPropertyNames.Code]),
        Annotations = new ToolAnnotations
        {
            Title = "Execute C# Code",
            DestructiveHint = true,
            OpenWorldHint = true
        }
    };

    public async Task<McpToolExecutionResult> ExecuteAsync(
        string payloadJson, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(payloadJson);
        if (!doc.RootElement.TryGetProperty(IpcPropertyNames.Code, out var codeElement) ||
            codeElement.ValueKind != JsonValueKind.String)
        {
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, "Missing required 'code' parameter.");
        }

        var code = codeElement.GetString();
        if (string.IsNullOrWhiteSpace(code))
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed, "Code parameter must not be empty.");

        ScriptCompilationResult? compilationResult = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CompileTimeout);

            try
            {
                compilationResult = await compiler
                    .CompileAsync(code!, scriptBridge, ct: timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return ErrorResult($"[COMPILATION ERROR] Timed out after {CompileTimeout.TotalSeconds}s. " +
                    "Simplify code or reduce #r nuget dependencies.");
            }

            if (!compilationResult.Success || compilationResult.Command is null)
            {
                var diagnostics = compilationResult.FormatDiagnostics();
                return ErrorResult($"[COMPILATION ERROR] Fix the code and retry.\n{diagnostics}");
            }

            var result = await hostContext
                .ExecuteAsync(() => commandRunner.RunCompiledCommand(compilationResult.Command), ct)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                var error = result.Message;
                var prefix = error.Contains("rolled back", StringComparison.OrdinalIgnoreCase)
                    ? "[ROLLBACK] Transaction failed due to unresolvable constraint.\n"
                    : "[RUNTIME ERROR] ";
                return ErrorResult($"{prefix}{error}");
            }

            var output = result.Message;
            var rollback = ExecutionGuardContext.RollbackSummary;
            if (!string.IsNullOrEmpty(rollback))
                output = $"{output}\n\n⚠️ {rollback}";

            var callResult = new CallToolResult
            {
                Content = [new TextContentBlock { Text = output }]
            };
            return McpToolExecutionResult.Completed(callResult, $"Completed '{Name}'.");
        }
        finally
        {
            DisposeCompilation(compilationResult);
        }
    }

    private McpToolExecutionResult ErrorResult(string text)
    {
        var errorResult = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text }]
        };
        return McpToolExecutionResult.Completed(errorResult, $"Failed '{Name}'.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisposeCompilation(ScriptCompilationResult? result)
    {
        result?.Cleanup?.Dispose();

#if NET
        GC.Collect();
        GC.WaitForPendingFinalizers();
#endif
    }
}
