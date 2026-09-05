namespace DevTools.Execution.Interfaces;

public interface IScriptExecutionStrategyFactory
{
    IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath);
}
