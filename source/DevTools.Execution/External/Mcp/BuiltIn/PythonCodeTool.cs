using System.ComponentModel;
using DevTools.Mcp.BuiltIn;
using ModelContextProtocol;
using DevTools.Execution.Providers.Python;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Executes inline Python code in the host process via Python.NET.</summary>
public sealed class PythonCodeTool(
    PythonInitializer initializer,
    PythonExecutor executor,
    IHostContextExecutor hostContext) : IBuiltInMcpTool
{
    public McpServerTool Primitive => McpServerTool.Create(typeof(PythonCodeTool).GetMethod(nameof(ExecutePythonAsync))!, this);

    [McpServerTool(Name = "execute_python_code")]
    [Description("Execute Python code in the running CAD/BIM host.")]
    public async Task<CallToolResult> ExecutePythonAsync(
        [Description("Python code with explicit host API imports.")] string code,
        [Description("Short description of what the code does for logging.")] string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new McpException("Code parameter must not be empty.");

        await initializer.InitializeAsync().ConfigureAwait(false);

        if (!initializer.IsInitialized)
        {
            return ErrorResult("Python runtime not initialized. Ensure pixi environment is set up.");
        }

        if (!await ResolveDepsAsync(code, cancellationToken).ConfigureAwait(false))
        {
            var detail = _lastDepError is not null
                ? $"[DEPENDENCY ERROR] {_lastDepError}"
                : "[DEPENDENCY ERROR] Failed to resolve or install PEP 723 dependencies.";
            _lastDepError = null;
            return ErrorResult(detail);
        }

        var result = await hostContext.ExecuteAsync(() => RunCode(code), cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            var errorResult = new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = $"[RUNTIME ERROR] {result.Output}" }]
            };
            return errorResult;
        }

        var output = result.Output;
        var rollback = ExecutionGuardContext.RollbackSummary;
        if (!string.IsNullOrEmpty(rollback))
            output = $"{output}\n\n⚠️ {rollback}";

        var callResult = new CallToolResult
        {
            Content = [new TextContentBlock { Text = output }]
        };
        return callResult;
    }

    private async Task<bool> ResolveDepsAsync(string code, CancellationToken ct)
    {
        var provider = initializer.Provider;
        if (provider is null) return true;

        try
        {
            var deps = await PythonDepsManager.ResolveDependenciesAsync(
                provider, code, ct).ConfigureAwait(false);
            if (deps.Count == 0) return true;

            await PythonDepsManager.InstallDependenciesAsync(
                provider, deps, new Progress<string>(_ => { }), ct).ConfigureAwait(false);

            PythonDepsManager.RefreshImportCache(initializer);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastDepError = ex.Message;
            return false;
        }
    }

    private string? _lastDepError;

    private static CallToolResult ErrorResult(string text) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = text }]
    };

    private PythonExecutionOutcome RunCode(string code)
    {
        return executor.Execute("execute_python_code", rootFolder: null, scope =>
        {
            scope.Set(PythonInstances.Source, new PyString(code));
            scope.Exec(StdoutCaptureBegin);
            try
            {
                scope.Exec("exec(compile(__source__, __file__, 'exec'), globals())");
            }
            catch (PythonException ex)
            {
                RestoreStdout(scope);
                var captured = GetCapturedOutput(scope);
                var error = string.IsNullOrEmpty(captured) ? ex.Message : $"{captured}\n{ex.Message}";
                return new PythonExecutionOutcome(false, error);
            }

            RestoreStdout(scope);
            var output = GetCapturedOutput(scope);
            return new PythonExecutionOutcome(true, output ?? "Code executed successfully.");
        });
    }

    private static void RestoreStdout(PyModule scope)
    {
        try { scope.Exec("sys.stdout, sys.stderr = __orig_out__, __orig_err__\nbuiltins.print = __orig_print__"); }
        catch { /* already restored or scope broken */ }
    }

    private static string GetCapturedOutput(PyModule scope)
    {
        try { return scope.Eval("__buf__.getvalue().strip()").As<string>() ?? ""; }
        catch { return ""; }
    }

    private const string StdoutCaptureBegin = """
        import sys, io, builtins
        __buf__ = io.StringIO()
        __orig_out__, __orig_err__ = sys.stdout, sys.stderr
        __orig_print__ = builtins.print
        sys.stdout = sys.stderr = __buf__
        builtins.print = lambda *a, **kw: __buf__.write(
            kw.get('sep', ' ').join(str(x) for x in a) + kw.get('end', '\n'))
        """;

    private sealed record PythonExecutionOutcome(bool Success, string Output);
}
