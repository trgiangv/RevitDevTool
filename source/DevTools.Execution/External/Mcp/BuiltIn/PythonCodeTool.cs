using System.ComponentModel;
using DevTools.Execution.Providers.Python;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Executes inline Python code in the host process via Python.NET.</summary>
public sealed class PythonCodeTool : IBuiltInMcpTool
{
    private readonly PythonInitializer _initializer;
    private readonly PythonExecutor _executor;
    private readonly IHostContextExecutor _hostContext;
    private string? _lastDepError;

    public PythonCodeTool(
        PythonInitializer initializer,
        PythonExecutor executor,
        IHostContextExecutor hostContext)
    {
        _initializer = initializer;
        _executor = executor;
        _hostContext = hostContext;
        ServerTool = McpServerTool.Create(
            ExecuteAsync,
            new McpServerToolCreateOptions
            {
                Name = "execute_python_code",
                Title = "Execute Python Code",
                Description =
                    "Execute Python code in the host process via Python.NET. " +
                    "Code runs in global scope with CLR references already added by host setup. " +
                    "Use `# /// script` header for external packages (PEP 723).\n" +
                    "RULES: Always include explicit imports for the host API namespace. " +
                    "Wrap logic in def run(): ... run(). Use print() for output.\n" +
                    "BEFORE WRITING CODE: Read python-cheatsheet resource for host API patterns.\n" +
                    "Error responses: [RUNTIME ERROR] check logic/imports.",
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => "execute_python_code";
    public McpServerTool ServerTool { get; }

    [McpMeta(McpTaskExecutionMeta.MetaKey, McpTaskExecutionMeta.Mode.Optional)]
    [Description("Execute Python code in the host process via Python.NET.")]
    private async Task<CallToolResult> ExecuteAsync(
        [Description(
            "Python code with explicit imports. Host CLR references are pre-loaded; " +
            "import the host API namespace you need (e.g. Autodesk.Revit or Autodesk.AutoCAD). " +
            "Add PEP 723 `# /// script` metadata for external packages.")]
        string code,
        [Description("Short description of what the code does (for logging).")]
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ToolHelpers.ErrorResult("Code parameter must not be empty.");

        await _initializer.InitializeAsync().ConfigureAwait(false);

        if (!_initializer.IsInitialized)
            return ToolHelpers.ErrorResult("Python runtime not initialized.");

        if (!await ResolveDepsAsync(code, cancellationToken).ConfigureAwait(false))
        {
            var detail = _lastDepError is not null
                ? $"[DEPENDENCY ERROR] {_lastDepError}"
                : "[DEPENDENCY ERROR] Failed to resolve or install PEP 723 dependencies.";
            _lastDepError = null;
            return ToolHelpers.ErrorResult(detail);
        }

        var result = await _hostContext.ExecuteAsync(() => RunCode(code), cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            return ToolHelpers.ErrorResult($"[RUNTIME ERROR] {result.Output}");

        var output = result.Output;
        var rollback = ExecutionGuardContext.RollbackSummary;
        if (!string.IsNullOrEmpty(rollback))
            output = $"{output}\n\n⚠️ {rollback}";

        return ToolHelpers.Result(output);
    }

    private async Task<bool> ResolveDepsAsync(string code, CancellationToken ct)
    {
        var provider = _initializer.Provider;
        if (provider is null) return true;

        try
        {
            var deps = await PythonDepsManager.ResolveDependenciesAsync(
                provider, code, ct).ConfigureAwait(false);
            if (deps.Count == 0) return true;

            await PythonDepsManager.InstallDependenciesAsync(
                provider, deps, new Progress<string>(_ => { }), ct).ConfigureAwait(false);

            PythonDepsManager.RefreshImportCache(_initializer);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _lastDepError = ex.Message;
            return false;
        }
    }

    private PythonExecutionOutcome RunCode(string code)
    {
        return _executor.Execute("execute_python_code", rootFolder: null, scope =>
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
            return new PythonExecutionOutcome(true, output);
        });
    }

    private static void RestoreStdout(PyModule scope)
    {
        try { scope.Exec("sys.stdout, sys.stderr = __orig_out__, __orig_err__\nbuiltins.print = __orig_print__"); }
        catch { /* already restored or scope broken */ }
    }

    private static string GetCapturedOutput(PyModule scope)
    {
        try { return scope.Eval("__buf__.getvalue().strip()").As<string>(); }
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
