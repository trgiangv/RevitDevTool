using System.Runtime.CompilerServices;
using System.ComponentModel;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Compiles and executes C# code in the host process via Roslyn.</summary>
public sealed class CSharpCodeTool(
    ICompiledScriptBridge scriptBridge,
    CSharpCompiler compiler,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner) : IBuiltInMcpTool
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    public McpServerTool Primitive => McpServerTool.Create(typeof(CSharpCodeTool).GetMethod(nameof(ExecuteAsync))!, this);

    [McpServerTool(Name = "execute_csharp_code")]
    [Description("Compile and execute C# code in the running CAD/BIM host.")]
    public async Task<CallToolResult> ExecuteAsync(
        [Description("Complete C# source code for the connected host.")] string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new McpException("Code parameter must not be empty.");

        ScriptCompilationResult? compilationResult = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(CompileTimeout);

            try
            {
                compilationResult = await compiler
                    .CompileAsync(code, scriptBridge, ct: timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
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
                .ExecuteAsync(() => commandRunner.RunCompiledCommand(compilationResult.Command), cancellationToken)
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
            return callResult;
        }
        finally
        {
            DisposeCompilation(compilationResult);
        }
    }

    private static CallToolResult ErrorResult(string text)
    {
        var errorResult = new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = text }]
        };
        return errorResult;
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
