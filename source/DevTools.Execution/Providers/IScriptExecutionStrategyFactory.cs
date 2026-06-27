using DevTools.Execution.Interfaces;

namespace DevTools.Execution.Providers;

public interface IScriptExecutionStrategyFactory
{
    IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath);
}
