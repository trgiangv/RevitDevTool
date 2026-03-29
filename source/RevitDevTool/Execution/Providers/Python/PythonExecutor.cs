using System.IO;
using Python.Runtime;
namespace RevitDevTool.Execution.Providers.Python;

/// <summary>
/// Script execution using Python.NET.
/// </summary>
public static class PythonExecutor
{
    public static void ExecuteScript(PythonInitializer initializer,
        string scriptPath,
        string scriptContent,
        string? rootFolder = null)
    {
        if (!initializer.IsInitialized)
            throw new InvalidOperationException("Python runtime not initialized. Call InitializeAsync() first.");

        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;
        using (Py.GIL())
        {
            if (initializer.GlobalScope == null)
                throw new InvalidOperationException("Global Python scope not initialized.");

            using (var scope = initializer.GlobalScope.NewScope())
            {
                scope.Set(PythonScopeVars.Source, new PyString(scriptContent));
                PrepareExecutionScope(scope, scriptPath, rootFolder);
                scope.Exec("""
                           compiled_code = compile(__source__, __file__, 'exec')
                           exec(compiled_code, globals())
                           """);
            }
        }
    }

    public static void PrepareExecutionScope(PyModule scope, string scriptPath, string? rootFolder = null)
    {
        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;
        scope.Set(PythonScopeVars.File, new PyString(scriptPath));
        scope.Set(PythonScopeVars.Root, new PyString(rootFolder));

        ResetModuleCache(scope);
        SetupScriptRoot(scope);
    }

    private static void SetupScriptRoot(PyModule scope)
    {
        const string setupCode = """
                                 import sys
                                 import os

                                 _rdt = sys.__revitdevtool__
                                 for prev in _rdt.get('added_roots', []):
                                     if prev in sys.path:
                                         sys.path.remove(prev)
                                 _rdt['added_roots'] = []

                                 if __root__:
                                     root = os.path.normcase(os.path.abspath(__root__))
                                     if root not in (os.path.normcase(p) for p in sys.path):
                                         sys.path.append(__root__)
                                     _rdt['added_roots'].append(__root__)
                                 """;
        scope.Exec(setupCode);
    }

    private static void ResetModuleCache(PyModule scope)
    {
        scope.Exec(PythonEmbedded.ResetScript);
    }
}
