using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Compiles and executes C# code in the host process via Roslyn.</summary>
public sealed class CSharpCodeTool : IBuiltInMcpTool
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    private readonly ICompiledScriptBridge _scriptBridge;
    private readonly CSharpCompiler _compiler;
    private readonly IHostContextExecutor _hostContext;
    private readonly ICommandRunner _commandRunner;

    public CSharpCodeTool(
        ICompiledScriptBridge scriptBridge,
        CSharpCompiler compiler,
        IHostContextExecutor hostContext,
        ICommandRunner commandRunner)
    {
        _scriptBridge = scriptBridge;
        _compiler = compiler;
        _hostContext = hostContext;
        _commandRunner = commandRunner;
        ServerTool = McpServerTool.Create(
            ExecuteAsync,
            new McpServerToolCreateOptions
            {
                Name = "execute_csharp_code",
                Title = "Execute C# Code",
                Description =
                    "Compile and execute C# code in the running host process. " +
                    "Host API assemblies auto-referenced. Use #r for extras, #r \"nuget:\" for packages.\n" +
                    "BEFORE WRITING CODE: Use search_dynamic / invoke_dynamic for resources with API patterns and model state.\n" +
                    "Error responses: [COMPILATION ERROR] fix code, [RUNTIME ERROR] check logic, [ROLLBACK] constraint violation.",
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => "execute_csharp_code";
    public McpServerTool ServerTool { get; }

    [McpMeta(McpTaskExecutionMeta.MetaKey, McpTaskExecutionMeta.Mode.Optional)]
    [Description("Compile and execute C# code in the running host process.")]
    private async Task<CallToolResult> ExecuteAsync(
        [Description(
            "Complete C# source. Revit: implement IExternalCommand, set 'message' ref param. " +
            "AutoCAD: use [CommandMethod]. Include all usings and attributes.")]
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ToolHelpers.ErrorResult("[COMPILATION ERROR] Code parameter must not be empty.");

        ScriptCompilationResult? compilationResult = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(CompileTimeout);

            try
            {
                compilationResult = await _compiler
                    .CompileAsync(code, _scriptBridge, ct: timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ToolHelpers.ErrorResult($"[COMPILATION ERROR] Timed out after {CompileTimeout.TotalSeconds}s. " +
                    "Simplify code or reduce #r nuget dependencies.");
            }

            if (!compilationResult.Success || compilationResult.Command is null)
            {
                var diagnostics = compilationResult.FormatDiagnostics();
                return ToolHelpers.ErrorResult($"[COMPILATION ERROR] Fix the code and retry.\n{diagnostics}");
            }

            var result = await _hostContext
                .ExecuteAsync(() => _commandRunner.RunCompiledCommand(compilationResult.Command), cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                var error = result.Message;
                var prefix = error.Contains("rolled back", StringComparison.OrdinalIgnoreCase)
                    ? "[ROLLBACK] Transaction failed due to unresolvable constraint.\n"
                    : "[RUNTIME ERROR] ";
                return ToolHelpers.ErrorResult($"{prefix}{error}");
            }

            var output = result.Message;
            var rollback = ExecutionGuardContext.RollbackSummary;
            if (!string.IsNullOrEmpty(rollback))
                output = $"{output}\n\n⚠️ {rollback}";

            return ToolHelpers.Result(output);
        }
        finally
        {
            DisposeCompilation(compilationResult);
        }
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
