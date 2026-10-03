using System.ComponentModel;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using DevTools.Mcp;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Executes inline Python code in the host process via Python.NET.</summary>
public sealed class PythonCodeTool : IBuiltInMcpTool
{
    private readonly PythonInitializer _initializer;
    private readonly IHostContextExecutor _hostContext;
    private string? _lastDepError;

    public PythonCodeTool(
        PythonInitializer initializer,
        IHostContextExecutor hostContext,
        IHostAppInfo hostApp)
    {
        _initializer = initializer;
        _hostContext = hostContext;
        ServerTool = McpServerTool.Create(
            ExecuteAsync,
            new McpServerToolCreateOptions
            {
                Name = McpSpecKeys.Tool.ExecutePython,
                Title = "Execute Python Code",
                Description = DescribeTool(hostApp.Host),
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => McpSpecKeys.Tool.ExecutePython;
    public McpServerTool ServerTool { get; }

    [Description("Execute Python code in the host process via Python.NET.")]
    private async Task<CallToolResult> ExecuteAsync(
        [Description("Python script for this host. Follow the pattern in this tool's description.")]
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
                ? $"{McpSpecKeys.Result.Dependency} {_lastDepError}"
                : $"{McpSpecKeys.Result.Dependency} Failed to resolve or install PEP 723 dependencies.";
            _lastDepError = null;
            return ToolHelpers.ErrorResult(detail);
        }

        var result = await _hostContext.ExecuteAsync(() => RunCode(code), cancellationToken).ConfigureAwait(false);

        if (!result.Success)
            return ToolHelpers.ErrorResult($"{McpSpecKeys.Result.Runtime} {result.Output}");

        var output = result.Output;
        var rollback = ExecutionGuardContext.RollbackSummary;
        if (!string.IsNullOrEmpty(rollback))
            output = $"{output}\n\n {rollback}";

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
        return PythonExecutor.Execute(_initializer, McpSpecKeys.Tool.ExecutePython, rootFolder: null, scope =>
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

    private static string DescribeTool(HostApp host) => host switch
    {
        HostApp.Revit =>
            "Execute one Python.NET script in Revit. " +
            $"Read {McpSpecKeys.Resource.RevitPythonCheatsheet} and send that pattern: explicit imports, def run(): ... run(), print() for output. " +
            "Read the document from RevitContext inside run(). " +
            "Packages use a # /// script header. " +
            $"This tool does not run IExternalCommand. For a compiled C# command, use {McpSpecKeys.Tool.ExecuteCSharp}. " +
            $"Errors: {McpSpecKeys.Result.Runtime} check logic/imports, {McpSpecKeys.Result.Dependency} fix the script header.",
        _ when host.IsAcadFamily() =>
            "Execute one Python.NET script in AutoCAD. " +
            $"Read {McpSpecKeys.Resource.AcadPythonCheatsheet} and send that pattern: explicit imports, def run(): ... run(), print() for output. " +
            "Lock the document inside run(). " +
            "Packages use a # /// script header. " +
            $"This tool does not run [CommandMethod]. For a compiled C# command, use {McpSpecKeys.Tool.ExecuteCSharp}. " +
            $"Errors: {McpSpecKeys.Result.Runtime} check logic/imports, {McpSpecKeys.Result.Dependency} fix the script header.",
        _ =>
            "Execute one Python.NET script in the host. " +
            "Wrap logic in def run(): ... run() and use print() for output. " +
            $"For a compiled C# command, use {McpSpecKeys.Tool.ExecuteCSharp}."
    };
}
