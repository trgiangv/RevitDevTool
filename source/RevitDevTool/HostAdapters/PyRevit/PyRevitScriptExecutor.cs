using System.Diagnostics;
using System.Reflection;
using DevTools.Execution.Models;
using RevitDevTool.Core;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace RevitDevTool.HostAdapters.PyRevit;

/// <summary>
/// Runs <c>*_ipy_script.py</c> through <c>PyRevitLoader.ScriptExecutor</c> (full pyRevit stack).
/// </summary>
internal static class PyRevitScriptExecutor
{
    internal static ExecutionResult Execute(string scriptPath, string rootPath)
    {
        var loader = PyRevitLibraryPaths.FindLoaderAssembly();
        if (loader is null)
            return ExecutionResult.Failed("pyRevit is not loaded in this Revit session.");

        var executorType = loader.GetType("PyRevitLoader.ScriptExecutor", throwOnError: false);
        if (executorType is null)
            return ExecutionResult.Failed("PyRevitLoader.ScriptExecutor was not found.");

        try
        {
            return Run(executorType, RevitContext.UiApplication, scriptPath, rootPath);
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed($"pyRevit execution failed: {ex.Message}", ex);
        }
    }

    private static ExecutionResult Run(
        Type executorType,
        UIApplication uiApplication,
        string scriptPath,
        string rootPath)
    {
        var executor = Activator.CreateInstance(executorType, uiApplication, false)
            ?? throw new InvalidOperationException("Could not create PyRevitLoader.ScriptExecutor.");

        var execute = executorType.GetMethod(
            "ExecuteScript",
            BindingFlags.Instance | BindingFlags.Public,
            binder: null,
            [typeof(string), typeof(IEnumerable<string>), typeof(string), typeof(IDictionary<string, object>)],
            modifiers: null) ?? throw new MissingMethodException(executorType.FullName, "ExecuteScript");

        var sysPaths = PyRevitSearchPaths.Build(scriptPath, rootPath);
        var revitResult = execute.Invoke(executor, [scriptPath, sysPaths, null, null]);
        var message = executorType
            .GetProperty("Message", BindingFlags.Instance | BindingFlags.Public)?
            .GetValue(executor) as string;

        if (!string.IsNullOrEmpty(message))
            Trace.Write(message);

        var resultName = revitResult?.ToString() ?? string.Empty;
        if (resultName.Contains("Succeeded", StringComparison.Ordinal))
            return ExecutionResult.Succeeded("Script completed (pyRevit).");

        if (!string.IsNullOrEmpty(message))
            return ExecutionResult.Failed(message!);

        return ExecutionResult.Failed($"pyRevit finished with {resultName}.");
    }
}
