using DevTools.Execution.Interfaces;
using DevTools.McpParser.Models;

namespace DevTools.Execution.Providers;

public interface IScriptExecutionStrategyFactory
{
    IExecutionStrategy Create(ExecutionMode mode, string scriptPath, string rootPath);
}
