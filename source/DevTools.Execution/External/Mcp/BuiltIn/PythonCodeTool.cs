using System.IO;
using System.Text.Json;
using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;
using DevTools.Mcp.Schema;
using ModelContextProtocol.Protocol;
using Python.Runtime;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Executes inline Python code in the host process via Python.NET.</summary>
public sealed class PythonCodeTool(
    PythonInitializer initializer,
    IHostContextExecutor hostContext) : IBuiltInMcpTool
{
    public string Name => "execute_python_code";

    public Tool ProtocolTool { get; } = new()
    {
        Name = "execute_python_code",
        Description =
            "Execute Python code in the host process via Python.NET. " +
            "Host builtins (DB, context helpers, print) are inherited from the runtime scope. " +
            "Use `# /// script` header for external packages (PEP 723).\n" +
            "RULES: Always wrap in def run(): ... run(). Do NOT create global variables. " +
            "Read active document/context inside functions, not at module level.\n" +
            "BEFORE WRITING CODE: Read available resources (list_dynamic_resources) for API patterns and model state.\n" +
            "Error responses: [RUNTIME ERROR] check logic/imports.",
        InputSchema = McpSchemaBuilder.Object(
        [
            McpSchemaBuilder.String(
                IpcPropertyNames.Code,
                "Python code. Wrap in def run(): ... run(). " +
                "Use print() for output. Use host API modules from the python-cheatsheet resource. " +
                "Add PEP 723 `# /// script` metadata for external packages."),
            McpSchemaBuilder.String(
                "description",
                "Short description of what the code does (for logging).")
        ],
        required: [IpcPropertyNames.Code]),
        Annotations = new ToolAnnotations
        {
            Title = "Execute Python Code",
            DestructiveHint = true,
            OpenWorldHint = true
        }
    };

    public async Task<McpToolExecutionResult> ExecuteAsync(string payloadJson, CancellationToken ct)
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

        await initializer.InitializeAsync().ConfigureAwait(false);

        if (!initializer.IsInitialized)
        {
            return McpToolExecutionResult.Failed(
                McpExecutionErrorCodes.ToolInvokeFailed,
                "Python runtime not initialized. Ensure pixi environment is set up.");
        }

        var scriptPath = Path.Combine(Path.GetTempPath(), $"mcp_{Guid.NewGuid():N}.py");
        try
        {
            await File.WriteAllTextAsync(scriptPath, code!, ct).ConfigureAwait(false);

            try
            {
                var depsOk = await PythonExecutionStrategy.ResolveDependenciesAsync(
                    initializer, scriptPath, progress: null, ct).ConfigureAwait(false);
                if (!depsOk)
                {
                    return McpToolExecutionResult.Failed(
                        McpExecutionErrorCodes.ToolInvokeFailed,
                        "Failed to resolve or install PEP 723 dependencies.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return McpToolExecutionResult.Failed(
                    McpExecutionErrorCodes.ToolInvokeFailed,
                    $"Package resolution failed: {ex.Message}");
            }

            var result = await hostContext.ExecuteAsync(() => ExecuteCode(code!), ct).ConfigureAwait(false);

            if (!result.Success)
            {
                var errorResult = new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = $"[RUNTIME ERROR] {result.Output}" }]
                };
                return McpToolExecutionResult.Completed(errorResult, $"Failed '{Name}'.");
            }

            var output = result.Output;
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
            try
            {
                if (File.Exists(scriptPath))
                    File.Delete(scriptPath);
            }
            catch
            {
                // Best-effort cleanup of temp script.
            }
        }
    }

    private PythonExecutionOutcome ExecuteCode(string code)
    {
        using (Py.GIL())
        {
            using var scope = initializer.GlobalScope!.NewScope();
            try
            {
                scope.Set("__user_code__", new PyString(code));
                scope.Exec(SandboxExecScript);

                var output = scope.Get("__captured_output__").As<string>();
                return new PythonExecutionOutcome(true, output ?? "(no output)");
            }
            catch (PythonException ex)
            {
                string captured;
                try
                {
                    scope.Exec(RestoreStdioScript);
                    captured = scope.Get("__captured_output__").As<string>();
                }
                catch
                {
                    captured = "";
                }

                var error = string.IsNullOrEmpty(captured)
                    ? ex.Message
                    : $"{captured}\n{ex.Message}";
                return new PythonExecutionOutcome(false, error);
            }
        }
    }

    private const string SandboxExecScript = """
        import sys, io, builtins
        __capture_buffer__ = io.StringIO()
        __orig_stdout__ = sys.stdout
        __orig_stderr__ = sys.stderr
        sys.stdout = __capture_buffer__
        sys.stderr = __capture_buffer__
        try:
            __sandbox__ = {}
            # Inject host builtins (set by IPythonBridge.SetupBuiltins)
            for __n__ in dir(builtins):
                if not __n__.startswith('_'):
                    __sandbox__[__n__] = getattr(builtins, __n__)
            if hasattr(builtins, '__revit__'):
                __sandbox__['__revit__'] = builtins.__revit__
            # Try-import common host modules (CLR refs added by setup scripts)
            try:
                from Autodesk.Revit import DB, UI
                from RevitDevTool.Core import RevitContext
                __sandbox__['DB'] = DB
                __sandbox__['UI'] = UI
                __sandbox__['RevitContext'] = RevitContext
            except ImportError:
                pass
            try:
                import Autodesk.AutoCAD.DatabaseServices as AcDb
                import Autodesk.AutoCAD.ApplicationServices as AcApp
                __sandbox__['AcDb'] = AcDb
                __sandbox__['AcApp'] = AcApp
            except ImportError:
                pass
            # Override print LAST to ensure capture (SetupRevit.py overrides builtins.print)
            __sandbox__['print'] = lambda *a, **kw: __capture_buffer__.write(
                kw.get('sep', ' ').join(str(x) for x in a) + kw.get('end', '\n'))
            exec(compile(__user_code__, "execute_python_code", "exec"), __sandbox__, __sandbox__)
        finally:
            sys.stdout = __orig_stdout__
            sys.stderr = __orig_stderr__
        __captured_output__ = __capture_buffer__.getvalue().strip() or "Code executed successfully."
        """;

    private const string RestoreStdioScript = """
        try:
            sys.stdout = __orig_stdout__
            sys.stderr = __orig_stderr__
            __captured_output__ = __capture_buffer__.getvalue().strip()
        except:
            __captured_output__ = ""
        """;

    private sealed record PythonExecutionOutcome(bool Success, string Output);
}
