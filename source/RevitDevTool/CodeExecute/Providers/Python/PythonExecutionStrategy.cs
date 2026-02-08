using RevitDevTool.CodeExecute.Services;
using RevitDevTool.Controllers;
using System.Diagnostics;
using RevitDevTool.CodeExecute.Interfaces;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Execution strategy for Python scripts.
/// Uses PythonExecutor for direct Python.NET execution.
/// Executes within Revit API context via ExternalEventController.
/// </summary>
public sealed class PythonExecutionStrategy : IExecutionStrategy
{
    private readonly string _scriptPath;
    private readonly string _rootPath;

    public PythonExecutionStrategy(string scriptPath, string rootPath)
    {
        _scriptPath = scriptPath;
        _rootPath = rootPath;
    }

    public void Execute()
    {
        // Initialize Python runtime synchronously if needed
        PythonExecutor.InitializeAsync().GetAwaiter().GetResult();

        // Execute script in Revit API context
        ExternalEventController.ActionEventHandler.Raise(_ =>
        {
            PythonExecutor.ExecuteScript(_scriptPath, _rootPath);
        });
    }
}