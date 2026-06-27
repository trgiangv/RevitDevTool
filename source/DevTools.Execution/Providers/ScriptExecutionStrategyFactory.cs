using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging;

namespace DevTools.Execution.Providers;

public sealed class ScriptExecutionStrategyFactory(
    PythonInitializer pythonInitializer,
    PythonExecutor pythonExecutor,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    CSharpCompilationCache csharpCache,
    FSharpCompilationCache fsharpCache,
    ILogger<CSharpExecutionStrategy> csharpLogger,
    ILogger<FSharpExecutionStrategy> fsharpLogger,
    ILogger<PythonExecutionStrategy> pythonExecutionLogger,
    ILogger<IronPythonExecutionStrategy> ironPythonLogger) : IScriptExecutionStrategyFactory
{
    public IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath) =>
        mode switch
        {
            ExecutionMode.Python => new PythonExecutionStrategy(
                scriptPath,
                rootPath,
                pythonInitializer,
                pythonExecutor,
                hostContext,
                pythonExecutionLogger),

            ExecutionMode.IronPython => new IronPythonExecutionStrategy(
                scriptPath,
                rootPath,
                ironPythonBridge,
                hostContext,
                ironPythonLogger),

            ExecutionMode.CSharp => new CSharpExecutionStrategy(
                scriptPath,
                hostContext,
                commandRunner,
                csharpCache,
                csharpLogger),

            ExecutionMode.FSharp => new FSharpExecutionStrategy(
                scriptPath,
                hostContext,
                commandRunner,
                fsharpCache,
                fsharpLogger),

            _ => throw new NotSupportedException($"Unsupported script execution mode '{mode}'.")
        };
}
