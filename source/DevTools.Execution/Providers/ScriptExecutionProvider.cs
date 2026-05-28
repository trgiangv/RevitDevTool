using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Providers;

/// <summary>Default script provider</summary>
public sealed class ScriptExecutionProvider(
    PythonInitializer pythonInitializer,
    PythonExecutor executor,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    ICompiledScriptBridge compiledScriptBridge)
    : ScriptExecutionProviderBase(
        pythonInitializer, executor, ironPythonBridge, hostContext, commandRunner, compiledScriptBridge);
