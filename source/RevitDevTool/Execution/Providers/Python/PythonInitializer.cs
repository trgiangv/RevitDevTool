using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Python.Runtime;
namespace RevitDevTool.Execution.Providers.Python;

public static class PythonInitializer
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    public static PyModule? GlobalScope { get; private set; }
    public static int DebugPort { get; private set; }

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
                    ListenToDebugger();
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

    private static void ListenToDebugger()
    {
        DebugPort = FindAvailablePort();

        const string debugpySetup = """
                                    import os
                                    import debugpy
                                    os.environ["PYDEVD_DISABLE_FILE_VALIDATION"] = "1"

                                    if not debugpy.is_client_connected():
                                        debugpy.listen(("localhost", __port__), in_process_debug_adapter=True)
                                    """;

        using var scope = Py.CreateScope();
        try
        {
            scope.Set("__port__", DebugPort);
            scope.Exec(debugpySetup);
        }
        catch (Exception e)
        {
            Trace.TraceError($"Failed to initialize debugpy: {e.Message}{Environment.NewLine}{e.StackTrace}");
        }
    }

    private static int FindAvailablePort(int preferredPort = 5678)
    {
        try
        {
            var tester = new TcpListener(IPAddress.Loopback, preferredPort);
            tester.Start();
            tester.Stop();
            return preferredPort;
        }
        catch (SocketException)
        {
            var fallback = new TcpListener(IPAddress.Loopback, 0);
            fallback.Start();
            var port = ((IPEndPoint) fallback.LocalEndpoint).Port;
            fallback.Stop();
            return port;
        }
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