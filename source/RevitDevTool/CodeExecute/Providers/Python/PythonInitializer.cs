using System.Diagnostics;
using System.IO;
using Python.Runtime;
using RevitDevTool.Settings;
namespace RevitDevTool.CodeExecute.Providers.Python;

public static class PythonInitializer
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    public static PyModule? GlobalScope { get; private set; }

    public static bool IsInitialized => PythonEngine.IsInitialized
                                         && PixiInstaller.IsPixiInstalled()
                                         && PixiEnvironment.IsEnvironmentReady();

    public static async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!PixiInstaller.IsPixiInstalled())
            {
                await PixiInstaller.SetupPixiAsync().ConfigureAwait(false);
            }
            
            if (!PixiEnvironment.IsEnvironmentReady())
            {
                await PixiEnvironment.SetupEnvironmentAsync().ConfigureAwait(false);
            }

            PixiEnvironment.ExtractParserScript();

            if (!PythonEngine.IsInitialized)
            {
                Runtime.PythonDLL = PixiEnvironment.GetPythonDllPath();
                PythonEngine.PythonHome = PixiEnvironment.PythonHome;
                PythonEngine.ProgramName = "RevitDevTool";
                PythonEngine.Initialize();
                PythonEngine.BeginAllowThreads();

                using (Py.GIL())
                {
                    SetupGlobalScope();
                }
            }
        }
        finally
        {
            InitLock.Release();
        }
    }

    private static void SetupGlobalScope()
    {
        GlobalScope ??= Py.CreateScope("__main__");
        
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
        builtins.__log_func__ = logFunction.ToPython();
        builtins.__revit__ = Context.UiApplication;

        var assembly = typeof(PythonInitializer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
                                   .FirstOrDefault(name => name.EndsWith("Setup.py", StringComparison.OrdinalIgnoreCase))!;
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var setupCode = reader.ReadToEnd();
        GlobalScope.Exec(setupCode);
    }

    /// <summary>
    /// Shutdown Python runtime on host/application shutdown.
    /// Do not call this per-script execution.
    /// </summary>
    public static void Shutdown()
    {
        if (!PythonEngine.IsInitialized) return;

        InitLock.Wait();
        try
        {
            using (Py.GIL())
            {
                GlobalScope?.Dispose();
                GlobalScope = null;
            }
            PythonEngine.Shutdown();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Python shutdown warning: {ex.Message}");
        }
        finally
        {
            InitLock.Release();
        }
    }
    
    public static void ListenToDebugger()
    {
        Task.Run(async () =>
        {
            await InitializeAsync().ConfigureAwait(true);
            var settingsService = Host.GetService<ISettingsService>();
            var port = settingsService.GeneralConfig.DebugPort;
            
            const string debugpySetup = """
                                        import os
                                        import debugpy
                                        os.environ["PYDEVD_DISABLE_FILE_VALIDATION"] = "1"

                                        if not debugpy.is_client_connected():
                                            debugpy.listen(("localhost", __port__), in_process_debug_adapter=True)
                                        """;

            using (Py.GIL())
            {
                using (var scope = Py.CreateScope())
                {
                    try
                    {
                        scope.Set("__port__", new PyInt(port));
                        scope.Exec(debugpySetup);
                        Trace.TraceInformation($"Debugpy listening on port {port}");
                    }
                    catch (Exception e)
                    {
                        Trace.TraceError($"Failed to initialize debugpy: {e.Message}{Environment.NewLine}{e.StackTrace}");
                    }
                }
            }
        });
    }

    /// <summary>
    /// Check if debugpy is connected to a debug adapter
    /// </summary>
    public static bool IsDebuggerConnected()
    {
        if (!IsInitialized) return false;
        
        using (Py.GIL())
        {
            using (var scope = Py.CreateScope())
            {
                try
                {
                    scope.Exec("""
                               import sys
                               __is_connected__ = False
                               if 'debugpy' in sys.modules:
                                   import debugpy
                                   __is_connected__ = debugpy.is_client_connected()
                               """);
                    dynamic isConnected = scope.Get("__is_connected__");
                    return (bool)isConnected;
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed to check debugger connection: {ex.Message}");
                    return false;
                }
            }
        }
    }
}