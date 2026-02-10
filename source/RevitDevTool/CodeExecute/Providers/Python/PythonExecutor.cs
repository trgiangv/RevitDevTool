using Python.Included;
using Python.Runtime;
using System.Diagnostics;
using System.IO;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Manages Python runtime initialization and script execution using Python.NET.
/// </summary>
public static class PythonExecutor
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);

    private static bool IsInitialized => PythonEngine.IsInitialized 
                                         && Installer.IsPythonInstalled() 
                                         && UvInstaller.IsUvInstalled();

    public static async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Installer.IsPythonInstalled())
            {
                await Installer.SetupPython().ConfigureAwait(false);
            }

            if (!UvInstaller.IsUvInstalled())
            {
                await UvInstaller.SetupUvAsync().ConfigureAwait(false);
            }
            
            if (!PythonEngine.IsInitialized)
            {
                Runtime.PythonDLL = Path.Combine(Installer.EmbeddedPythonHome, $"{Installer.PYTHON_VERSION}.dll");
                PythonEngine.PythonHome = Installer.EmbeddedPythonHome;
                PythonEngine.ProgramName = "RevitDevTool";
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();
            }
        }
        finally
        {
            InitLock.Release();
        }
    }

    public static void ExecuteScript(string scriptPath, string? rootFolder = null)
    {
        ValidateRuntime(scriptPath);

        var code = File.ReadAllText(scriptPath);
        rootFolder ??= Path.GetDirectoryName(scriptPath) ?? string.Empty;

        using (Py.GIL())
        {
            using (var scope = Py.CreateScope("__main__"))
            {
                SetupScopeVariables(scope, scriptPath, rootFolder);
                SetupOutputRedirection(scope);

                try
                {
                    scope.Exec(code);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(ex.Message + Environment.NewLine + ex.StackTrace);
                }
            }
        }
    }

    private static void ValidateRuntime(string scriptPath)
    {
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException($"Python script not found: {scriptPath}");

        if (!IsInitialized)
            throw new InvalidOperationException("Python runtime not initialized. Call InitializeAsync() first.");
    }

    private static void SetupScopeVariables(PyModule scope, string scriptPath, string rootFolder)
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
        
        scope.Set("__file__", scriptPath);
        scope.Set("__root__", rootFolder);
        scope.Set("__revit__", Context.UiApplication);
        scope.Set("__log_func__", logFunction.ToPython());
    }

    private static void SetupOutputRedirection(PyModule scope)
    {
        const string setupCode = """
                                 import sys
                                 import builtins
                                 import clr

                                 clr.AddReference("RevitAPI")
                                 clr.AddReference('RevitAPIUI')
                                 clr.AddReference("AdWindows")
                                 clr.AddReference("UIFramework")
                                 clr.AddReference("UIFrameworkServices")

                                 if int(__revit__.Application.VersionNumber) >= 2024:
                                     clr.AddReference("Microsoft.Web.WebView2.Wpf")
                                     clr.AddReference("Microsoft.Web.WebView2.Core")

                                 if int(__revit__.Application.VersionNumber) >= 2025:
                                     clr.AddReference("System.Console")
                                     clr.AddReference("System.Diagnostics.TraceSource") 

                                 if __root__ not in sys.path:
                                     sys.path.append(__root__)
                                 
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
}
