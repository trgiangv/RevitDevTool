using System.IO;
using Python.Runtime;
namespace DevTools.Execution.Providers.Python;

public class PythonExecutor(PythonInitializer initializer)
{
    /// <summary>
    /// Execute a callback within a fresh Python scope.
    /// When <paramref name="rootFolder"/> is provided, resets module cache and configures sys.path
    /// (for file-based scripts). When null, creates a minimal scope (for inline MCP code).
    /// </summary>
    public T Execute<T>(
        string anchorFileOrLabel,
        string? rootFolder,
        Func<PyModule, T> action)
    {
        using (Py.GIL())
        {
            if (!initializer.IsInitialized)
                throw new InvalidOperationException("Python runtime not initialized.");

            using var scope = initializer.GlobalScope!.NewScope();
            scope.Set(PythonInstances.File, new PyString(anchorFileOrLabel));

            if (rootFolder is not null)
            {
                scope.Set(PythonInstances.Root, new PyString(rootFolder));
                ResetModuleCache(scope);
                SetupScriptRoot(scope);
            }

            return action(scope);
        }
    }

    private static void SetupScriptRoot(PyModule scope)
    {
        const string setupCode = """
                                 import sys
                                 import os

                                 _rdt = sys.__devtool__
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