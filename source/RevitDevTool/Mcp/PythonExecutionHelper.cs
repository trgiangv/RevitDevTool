using System.IO;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.Mcp;

internal static class PythonExecutionHelper
{
    public static string InvokeScript(string sourcePath, Action<PyModule> configureScope)
    {
        PythonInitializer.InitializeAsync().GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        using (Py.GIL())
        {
            if (PythonInitializer.GlobalScope is null)
                throw new InvalidOperationException("Global Python scope not initialized.");

            using var scope = PythonInitializer.GlobalScope.NewScope();
            PythonExecutor.PrepareExecutionScope(scope, sourcePath);
            configureScope(scope);
            scope.Exec(PythonEmbedded.ToolInvokeScript);
            return scope.Get("__result_json__").As<string>();
        }
    }
}
