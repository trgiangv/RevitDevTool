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
                                         && PythonInstaller.IsPixiInstalled()
                                         && PythonEnvironment.IsEnvironmentReady();

    public static async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await InitLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsInitialized) return;

            if (!PythonInstaller.IsPixiInstalled())
            {
                await PythonInstaller.SetupPixiAsync().ConfigureAwait(false);
            }

            if (!PythonEnvironment.IsEnvironmentReady())
            {
                await PythonEnvironment.SetupEnvironmentAsync().ConfigureAwait(false);
            }

            if (!PythonEngine.IsInitialized)
            {
                PrependPixiEnvToPath();

                Runtime.PythonDLL = PythonEnvironment.GetPythonDllPath();
                PythonEngine.PythonHome = PythonEnvironment.PythonHome;
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
        catch (TypeInitializationException ex)
        {
            Trace.TraceError(
                $"[Python] Fatal init failure (pythonnet DLL load). " +
                $"Cause: {ex.InnerException?.Message ?? ex.Message}\n" +
                $"{ex.InnerException?.StackTrace ?? ex.StackTrace}");
        }
        catch (Exception ex)
        {
            Trace.TraceError($"[Python] Fatal init failure: {ex.Message}\n{ex.StackTrace}");
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

    /// <summary>
    /// Prepend the pixi Python env directory (and Library/bin) to the process PATH
    /// so that Windows DLL loader can find python313.dll's native dependencies
    /// (vcruntime140.dll, python3.dll, etc.) which live alongside the interpreter.
    /// Must be called before Runtime.PythonDLL and PythonEngine.PythonHome are set.
    /// </summary>
    private static void PrependPixiEnvToPath()
    {
        var pythonHome = PythonEnvironment.PythonHome;
        var libraryBin = Path.Combine(pythonHome, "Library", "bin");

        var current = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        var toAdd = new[] { pythonHome, libraryBin }
            .Where(Directory.Exists)
            .Where(d => current.IndexOf(d, StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();

        if (toAdd.Count == 0) return;

        var newPath = string.Join(";", toAdd) + ";" + current;
        Environment.SetEnvironmentVariable("PATH", newPath);
        Trace.TraceInformation($"[Python] Prepended to PATH: {string.Join("; ", toAdd)}");
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

        GlobalScope.Exec(PythonEmbedded.SetupScript);
    }
}
