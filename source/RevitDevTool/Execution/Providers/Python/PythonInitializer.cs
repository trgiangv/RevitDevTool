using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Python.Runtime;
using RevitDevTool.Core;

namespace RevitDevTool.Execution.Providers.Python;

public sealed class PythonInitializer(
    [FromKeyedServices(PythonBackend.Pixi)] PyEnvironmentProvider pixiProvider,
    [FromKeyedServices(PythonBackend.Pip)] PyEnvironmentProvider pipProvider)
{
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public PyModule? GlobalScope { get; private set; }
    public PyEnvironmentProvider? Provider { get; private set; }

    public bool IsInitialized => PythonEngine.IsInitialized
                                 && Provider is not null
                                 && Provider.IsEnvironmentReady();

    public async Task InitializeAsync()
    {
        if (IsInitialized) return;

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsInitialized) return;

            Provider ??= await DetectProviderAsync().ConfigureAwait(false);
            Trace.TraceInformation($"[Python] Using backend: {Provider.Backend}");

            if (!Provider.IsEnvironmentReady())
            {
                await Provider.SetupEnvironmentAsync().ConfigureAwait(false);
            }

            if (!PythonEngine.IsInitialized)
            {
                PrependPythonHomeToPath(Provider.PythonHome);

                Runtime.PythonDLL = Provider.GetPythonDllPath();
                PythonEngine.PythonHome = Provider.PythonHome;
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
        catch (Exception ex)
        {
            Trace.TraceError($"[Python] Fatal init failure: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task Shutdown()
    {
        if (!PythonEngine.IsInitialized) return;

        await _initLock.WaitAsync().ConfigureAwait(false);
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
            _initLock.Release();
        }
    }

    private async Task<PyEnvironmentProvider> DetectProviderAsync()
    {
        try
        {
            await PythonInstaller.SetupPixiAsync().ConfigureAwait(false);
            Trace.TraceInformation("[Python] Pixi is available.");
            return pixiProvider;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"[Python] Pixi unavailable ({ex.GetType().Name}: {ex.Message}). Falling back to pip.");
            return pipProvider;
        }
    }

    private static void PrependPythonHomeToPath(string pythonHome)
    {
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

    private void SetupGlobalScope()
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
