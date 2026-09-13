using DevTools.Execution.Models;
using Microsoft.Extensions.Logging;
namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// pyRevit script entry: Labs <c>ScriptRuntime</c> when available, otherwise <c>PyRevitLoader</c>.
/// </summary>
internal static class PyRevitScriptExecutor
{
    internal static ExecutionResult Execute(string scriptPath, string rootPath, ILogger? logger = null)
    {
        try
        {
            PyRevitAssemblyLoader.EnsureLoaded(scriptPath, logger);
            var reflection = PyRevitReflectionCache.Instance;

            if (reflection.HasRuntime)
                return reflection.ExecuteRuntime(scriptPath, rootPath);

            if (reflection.HasLoader)
                return reflection.ExecuteLoader(scriptPath, rootPath);

            return ExecutionResult.Failed("pyRevit is not loaded in this Revit session.");
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed($"pyRevit execution failed: {ex.Message}", ex);
        }
    }
}
