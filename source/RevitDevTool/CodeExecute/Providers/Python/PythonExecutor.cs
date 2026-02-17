using Python.Runtime;
using System.Diagnostics;
using System.IO;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Manages Python runtime initialization and script execution using Python.NET.
/// </summary>
public static class PythonExecutor
{
    public static void ExecuteScript(string scriptPath, string scriptContent, string? rootFolder = null)
    {
        if (!PythonInitializer.IsInitialized)
            throw new InvalidOperationException("Python runtime not initialized. Call InitializeAsync() first.");

        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;
        using (Py.GIL())
        {
            using (var scope = Py.CreateScope("__main__"))
            {
                try
                {
                    SetupScopeVariables(scope, scriptPath, scriptContent, rootFolder);
                    ResetExecutionModuleCache(scope);
                    SetupOutputRedirection(scope);
                    scope.Exec("""
                               compiled_code = compile(__source__, __file__, 'exec')
                               exec(compiled_code, globals())
                               """);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message + Environment.NewLine + ex.StackTrace);
                }
            }
        }
    }

    private static void SetupScopeVariables(PyModule scope, string scriptPath, string scriptContent, string rootFolder)
    {
        Action<object> logFunction = obj =>
        {
            if (obj is string str)
            {
                Trace.Write(str);
            }
            else
            {
                Trace.Write(obj);
            }
        };
        
        dynamic builtins = Py.Import("builtins");
        builtins.__revit__ = Context.UiApplication;
        
        scope.Set("__source__", new PyString(scriptContent));
        scope.Set("__file__", new PyString(scriptPath));
        scope.Set("__root__", new PyString(rootFolder));
        scope.Set("__log_func__", logFunction.ToPython());
    }

    private static void SetupOutputRedirection(PyModule scope)
    {
        const string setupCode = """
                                 import sys
                                 import os
                                 import builtins
                                 import clr
                                 import site

                                 clr.AddReference("RevitAPI")
                                 clr.AddReference('RevitAPIUI')
                                 clr.AddReference("AdWindows")
                                 clr.AddReference("UIFramework")
                                 clr.AddReference("UIFrameworkServices")
                                 clr.AddReference("Revit.Async")

                                 if int(__revit__.Application.VersionNumber) >= 2024:
                                     clr.AddReference("Microsoft.Web.WebView2.Wpf")
                                     clr.AddReference("Microsoft.Web.WebView2.Core")

                                 if int(__revit__.Application.VersionNumber) >= 2025:
                                     clr.AddReference("System.Console")
                                     clr.AddReference("System.Diagnostics.TraceSource") 

                                 if __root__ not in sys.path:
                                     sys.path.append(__root__)

                                 # Ensure newly-installed packages are importable in the same runtime session.
                                 site_packages = os.path.join(sys.prefix, "Lib", "site-packages")
                                 if site_packages and os.path.isdir(site_packages) and site_packages not in sys.path:
                                     site.addsitedir(site_packages)
                                 
                                 def custom_print(*args, sep=' ', end='\n'):
                                     # To use Trace Visualization, pass objects as separate arguments: print("Label", obj)
                                 
                                     # Case 1: Single Argument -> Pass Raw Object (Enable Trace)
                                     if len(args) == 1:
                                         __log_func__(args[0])
                                         if end != '\n': 
                                             __log_func__(end)
                                         return
                                 
                                     # Case 2: Mixed Content containing Complex Objects
                                     # If we just str(obj), we lose Trace ability. 
                                     # If using default separator, we split them into separate logs to preserve objects.
                                     has_complex = any(not isinstance(a, (str, int, float, bool, type(None))) for a in args)
                                     
                                     if has_complex and sep == ' ':
                                         for arg in args:
                                             __log_func__(arg)
                                         if end != '\n': 
                                             __log_func__(end)
                                         return
                                 
                                     # Case 3: Simple Text or Custom Separator -> Standard Join
                                     text = sep.join(str(arg) for arg in args) + end
                                     __log_func__(text)
                                 
                                 # Override built-in print
                                 builtins.print = custom_print
                                 
                                 # Redirect stdout/stderr
                                 class StdOutRedirector:
                                     def __init__(self, log_func):
                                         self.log_func = log_func
                                     def write(self, text):
                                         # Avoid empty newlines from being logged separately if possible
                                         if text != '\n':
                                             self.log_func(text)
                                     def flush(self):
                                         pass
                                 
                                 sys.stdout = StdOutRedirector(__log_func__)
                                 sys.stderr = StdOutRedirector(__log_func__)
                                 """;
        scope.Exec(setupCode);
    }

    /// <summary>
    /// Python runtime is process-wide and keeps module cache in sys.modules.
    /// Clear modules that belong to the current script root before each execution
    /// so code changes are always reflected on next run.
    /// </summary>
    private static void ResetExecutionModuleCache(PyModule scope)
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
                                    to_remove = []

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
                                                    to_remove.append(name)
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
