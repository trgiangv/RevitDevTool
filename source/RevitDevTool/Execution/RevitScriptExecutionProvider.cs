using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.Python;

namespace RevitDevTool.Execution;

/// <summary>Revit script discovery with pyRevit-first IronPython execution.</summary>
public sealed class RevitScriptExecutionProvider(
    PythonInitializer pythonInitializer,
    PythonExecutor executor,
    IIronPythonBridge ironPythonBridge,
    IHostContextExecutor hostContext,
    ICommandRunner commandRunner,
    CSharpCompilationCache csharpCache,
    FSharpCompilationCache fsharpCache) : ScriptExecutionProviderBase(pythonInitializer, executor, ironPythonBridge, hostContext, commandRunner, csharpCache, fsharpCache)
{
    private readonly IIronPythonBridge _bridge = ironPythonBridge;
    private readonly IHostContextExecutor _hostContext = hostContext;

    protected override IExecutionStrategy CreateIronPythonStrategy(string scriptPath, string rootPath) =>
        new RevitIPyExecutionStrategy(scriptPath, rootPath, _bridge, _hostContext);
}
