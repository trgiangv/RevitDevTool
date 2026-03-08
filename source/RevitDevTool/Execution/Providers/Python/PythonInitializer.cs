using System.Diagnostics;
using System.IO;
using Python.Runtime;
using RevitDevTool.Core;
namespace RevitDevTool.Execution.Providers.Python;

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
                    PythonDebugger.StartListening();
                }
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

    private static void SetupGlobalScope()
    {
        GlobalScope ??= Py.CreateScope("__main__");

        Action<object> logFunction = obj =>
        {
            if (obj is string str)
                Trace.Write(str);
            else
                Trace.Write(obj);
        };

        dynamic builtins = Py.Import("builtins");
        builtins.__log_func__ = logFunction.ToPython();
        builtins.__revit__ = RevitContext.UiApplication;

        var assembly = typeof(PythonInitializer).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
                                   .FirstOrDefault(name => name.EndsWith("Setup.py", StringComparison.OrdinalIgnoreCase))!;
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        var setupCode = reader.ReadToEnd();
        GlobalScope.Exec(setupCode);
    }
}