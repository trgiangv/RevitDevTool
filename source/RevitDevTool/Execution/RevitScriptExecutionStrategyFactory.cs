using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
namespace RevitDevTool.Execution;

public sealed class RevitScriptExecutionStrategyFactory(
    ScriptExecutionStrategyFactory defaultFactory,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext) : IScriptExecutionStrategyFactory
{
    public IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath) =>
        mode == ExecutionMode.IronPython
            ? new RevitIPyExecutionStrategy(scriptPath, rootPath, ironPythonBridge, hostContext)
            : defaultFactory.Create(mode, scriptPath, rootPath);
}
