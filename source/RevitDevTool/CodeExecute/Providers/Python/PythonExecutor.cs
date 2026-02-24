using Python.Runtime;
using System.Diagnostics;
using System.IO;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Script execution using Python.NET.
/// </summary>
public static class PythonExecutor
{
    public static void ExecuteScript(string scriptPath, 
        string scriptContent,
        string? rootFolder = null,
        Document? document = null,
        bool throwOnError = false)
    {
        if (!PythonInitializer.IsInitialized)
            throw new InvalidOperationException("Python runtime not initialized. Call InitializeAsync() first.");

        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;
        using (Py.GIL())
        {
            if (PythonInitializer.GlobalScope == null)
                throw new InvalidOperationException("Global Python scope not initialized.");

            using (var scope = PythonInitializer.GlobalScope.NewScope())
            {
                try
                {
                    scope.Set("__source__", new PyString(scriptContent));
                    scope.Set("__file__", new PyString(scriptPath));
                    scope.Set("__root__", new PyString(rootFolder));
                    
                    if (document != null)
                    {
                        scope.Set("__doc__", document.ToPython());
                    }
                    
                    ResetModuleCache(scope);
                    SetupScriptRoot(scope);
                    scope.Exec("""
                               compiled_code = compile(__source__, __file__, 'exec')
                               exec(compiled_code, globals())
                               """);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message + Environment.NewLine + ex.StackTrace);
                    if (throwOnError) throw;
                }
            }
        }
    }

    /// <summary>
    /// Manage sys.path so the script's root folder is importable.
    /// Removes all previously added roots before adding the new one,
    /// preventing stale paths from accumulating across executions.
    /// </summary>
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

    /// <summary>
    /// Python runtime is process-wide and keeps module cache in sys.modules.
    /// Clear modules that belong to the current script root before each execution
    /// so that code changes are reflected without restarting Revit.
    /// </summary>
    private static void ResetModuleCache(PyModule scope)
    {
        const string resetCode = """
                                import os
                                import sys
                                import importlib

                                root = os.path.abspath(__root__) if __root__ else ""
                                script_file = os.path.abspath(__file__) if __file__ else ""
                                script_dir = os.path.dirname(script_file)
                                targets = [p for p in (root, script_dir) if p]

                                if targets:
                                    normalized_targets = [os.path.normcase(p) for p in targets]
                                    to_remove = set()

                                    for name, mod in sys.modules.items():
                                        path = getattr(mod, "__file__", None)
                                        if not path:
                                            continue

                                        try:
                                            mod_path = os.path.normcase(os.path.abspath(path))
                                        except Exception:
                                            continue

                                        for target in normalized_targets:
                                            try:
                                                if os.path.commonpath([mod_path, target]) == target:
                                                    to_remove.add(name)
                                                    break
                                            except Exception:
                                                continue
                                    for name in to_remove:
                                        sys.modules.pop(name, None)

                                importlib.invalidate_caches()
                                """;
        scope.Exec(resetCode);
    }
}
