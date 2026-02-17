using System.Diagnostics;
using System.IO;
using Python.Included;
using Python.Runtime;
using RevitDevTool.Settings;
namespace RevitDevTool.CodeExecute.Providers.Python;

public static class PythonInitializer
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    public static bool IsInitialized => PythonEngine.IsInitialized 
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
            if (PythonEngine.IsInitialized)
            {
                PythonEngine.Shutdown();
            }
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
                                        # /// script
                                        # dependencies = [
                                        #     "debugpy",
                                        # ]
                                        # ///

                                        import os
                                        import debugpy
                                        os.environ["PYDEVD_DISABLE_FILE_VALIDATION"] = "1"

                                        if not debugpy.is_client_connected():
                                            debugpy.listen(("localhost", __port__), in_process_debug_adapter=True)
                                        """;
            var success = await PythonExecutionStrategy.ResolveDependenciesAsync(debugpySetup).ConfigureAwait(true);

            if (!success)
            {
                Trace.TraceWarning("Debugpy setup cancelled: Dependency resolution failed or was cancelled by user.");
                return;
            }
            
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

        try
        {
            using (Py.GIL())
            {
                using (var scope = Py.CreateScope())
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
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to check debugger connection: {ex.Message}");
            return false;
        }
    }
}