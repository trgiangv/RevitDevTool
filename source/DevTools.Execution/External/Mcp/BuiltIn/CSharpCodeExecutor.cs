using System.IO;
using System.Runtime.CompilerServices;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>
/// Compiles and executes C# code via Roslyn, then runs the command on the host thread.
/// Compiled assemblies are disposed after each invocation.
/// </summary>
public sealed class CSharpCodeExecutor(
    ICompiledScriptBridge scriptBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner)
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    public async Task<CodeExecutionResult> ExecuteAsync(string code, CancellationToken ct = default)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"mcp_{Guid.NewGuid():N}_script.csx");
        ScriptCompilationResult? compilationResult = null;
        try
        {
            await File.WriteAllTextAsync(tempFile, code, ct).ConfigureAwait(false);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(CompileTimeout);

            compilationResult = await CSharpCompiler
                .CompileAsync(tempFile, scriptBridge, ct: timeoutCts.Token)
                .ConfigureAwait(false);

            if (!compilationResult.Success || compilationResult.Command is null)
            {
                var diagnostics = compilationResult.FormatDiagnostics();
                return CodeExecutionResult.CompilationError(diagnostics);
            }

            var result = await hostContext
                .ExecuteAsync(() => commandRunner.RunCompiledCommand(compilationResult.Command), ct)
                .ConfigureAwait(false);

            return result.Success
                ? CodeExecutionResult.Success(result.Message)
                : CodeExecutionResult.RuntimeError(result.Message);
        }
        finally
        {
            DisposeCompilation(compilationResult);

            try { File.Delete(tempFile); }
            catch { /* best-effort cleanup */ }
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

public sealed record CodeExecutionResult
{
    public bool IsSuccess { get; private init; }
    public string Output { get; private init; } = string.Empty;
    public string? Error { get; private init; }

    public static CodeExecutionResult Success(string output)
        => new() { IsSuccess = true, Output = output };

    public static CodeExecutionResult CompilationError(string diagnostics)
        => new() { IsSuccess = false, Error = diagnostics, Output = string.Empty };

    public static CodeExecutionResult RuntimeError(string message)
        => new() { IsSuccess = false, Error = message, Output = string.Empty };
}
