using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.IronPython;
using Microsoft.Extensions.Logging;
namespace RevitDevTool.Execution;

public sealed class RevitScriptExecutionStrategyFactory(
    ScriptExecutionStrategyFactory defaultFactory,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ILogger<IronPythonExecutionStrategy> ironPythonLogger,
    ILogger<RevitIPyExecutionStrategy> revitIPyLogger) : IScriptExecutionStrategyFactory
{
    public IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath) =>
        mode == ExecutionMode.IronPython
            ? new RevitIPyExecutionStrategy(scriptPath, rootPath, ironPythonBridge, hostContext, ironPythonLogger, revitIPyLogger)
            : defaultFactory.Create(mode, scriptPath, rootPath);
}
