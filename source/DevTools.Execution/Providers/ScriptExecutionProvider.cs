using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Providers;

/// <summary>Default script provider</summary>
public sealed class ScriptExecutionProvider(
    PythonInitializer pythonInitializer,
    PythonExecutor executor,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    CSharpCompilationCache csharpCache,
    FSharpCompilationCache fsharpCache)
    : ScriptExecutionProviderBase(
        pythonInitializer, executor, ironPythonBridge, hostContext, commandRunner, csharpCache, fsharpCache);
