using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Providers;

public sealed class ScriptExecutionStrategyFactory(
    PythonInitializer pythonInitializer,
    PythonExecutor pythonExecutor,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    CSharpCompilationCache csharpCache,
    FSharpCompilationCache fsharpCache) : IScriptExecutionStrategyFactory
{
    public IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath) =>
        mode switch
        {
            ExecutionMode.Python => new PythonExecutionStrategy(
                scriptPath,
                rootPath,
                pythonInitializer,
                pythonExecutor,
                hostContext),

            ExecutionMode.IronPython => new IronPythonExecutionStrategy(
                scriptPath,
                rootPath,
                ironPythonBridge,
                hostContext),

            ExecutionMode.CSharp => new CSharpExecutionStrategy(
                scriptPath,
                hostContext,
                commandRunner,
                csharpCache),

            ExecutionMode.FSharp => new FSharpExecutionStrategy(
                scriptPath,
                hostContext,
                commandRunner,
                fsharpCache),

            _ => throw new NotSupportedException($"Unsupported script execution mode '{mode}'.")
        };
}
