using System.IO;
using Python.Runtime;
using RevitDevTool.Execution.Providers.Python;

namespace RevitDevTool.Mcp;

internal static class PythonExecutionHelper
{
    public static string InvokeScript(PythonInitializer initializer, string sourcePath, Action<PyModule> configureScope)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            throw new InvalidOperationException($"Python MCP source file was not found: {sourcePath}.");

        using (Py.GIL())
        {
            if (initializer.GlobalScope is null)
                throw new InvalidOperationException("Global Python scope not initialized.");

            using var scope = initializer.GlobalScope.NewScope();
            PythonExecutor.PrepareExecutionScope(scope, sourcePath);
            configureScope(scope);
            scope.Exec(PythonEmbedded.ToolInvokeScript);
            return scope.Get(PythonScopeVars.ResultJson).As<string>();
        }
    }
}
