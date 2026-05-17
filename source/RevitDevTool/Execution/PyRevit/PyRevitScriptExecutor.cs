using DevTools.Execution.Models;
using RevitDevTool.Core;
namespace RevitDevTool.Execution.PyRevit;

/// <summary>
/// pyRevit script entry: Labs <c>ScriptRuntime</c> when available, otherwise <c>PyRevitLoader</c>.
/// </summary>
internal static class PyRevitScriptExecutor
{
    internal static ExecutionResult Execute(string scriptPath, string rootPath)
    {
        var uiApplication = RevitContext.UiApplication;
        try
        {
            var reflection = PyRevitReflectionCache.Instance;

            if (reflection.HasRuntime)
                return reflection.ExecuteRuntime(scriptPath, rootPath, uiApplication);

            if (reflection.HasLoader)
                return reflection.ExecuteLoader(scriptPath, rootPath, uiApplication);

            return ExecutionResult.Failed("pyRevit is not loaded in this Revit session.");
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed($"pyRevit execution failed: {ex.Message}", ex);
        }
    }
}
