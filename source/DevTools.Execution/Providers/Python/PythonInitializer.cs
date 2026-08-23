using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;
namespace DevTools.Execution.Providers.Python;

public sealed class PythonInitializer(
    [FromKeyedServices(PythonBackend.Pixi)] PyEnvironmentProvider pixiProvider,
    [FromKeyedServices(PythonBackend.Pip)] PyEnvironmentProvider pipProvider,
    IPythonBridge bridge,
    ILogger<PythonInitializer> logger)
{
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public PyModule? GlobalScope { get; private set; }
    public PyEnvironmentProvider? Provider { get; private set; }

    public bool IsInitialized => PythonEngine.IsInitialized
                                 && Provider?.IsEnvironmentReady() == true;

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsInitialized) return;

            Provider ??= await DetectProviderAsync().ConfigureAwait(false);
            logger.ZLogInformation($"Using backend: {Provider.Backend}");

            if (!Provider.IsEnvironmentReady())
            {
                await Provider.SetupEnvironmentAsync().ConfigureAwait(false);
            }

            if (PythonEngine.IsInitialized) return;

            if (IsHostPythonRuntimeLoaded())
            {
                logger.ZLogWarning(
                    $"Host embedded Python is already loaded in this process (e.g. Plant 3D). Skipping DevTools Python initialization to avoid a dual-runtime crash. Python scripts are unavailable in this session.");
                return;
            }

            PrependPythonHomeToPath(Provider.PythonHome);

            Runtime.PythonDLL = Provider.GetPythonDllPath();
            PythonEngine.PythonHome = Provider.PythonHome;
            PythonEngine.ProgramName = bridge.ProgramName;
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();

            using (Py.GIL())
            {
                SetupGlobalScope();
                PythonDebugger.StartListening(logger);
            }
        }
        catch (Exception ex)
        {
            logger.ZLogError($"Fatal init failure: {ex.Message}\n{ex.StackTrace}");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task ShutdownAsync()
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
            logger.ZLogWarning($"Python shutdown warning: {ex.Message}");
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
            await PythonInstaller.SetupPixiAsync(logger).ConfigureAwait(false);
            logger.ZLogInformation($"Pixi is available.");
            return pixiProvider;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"Pixi unavailable ({ex.GetType().Name}: {ex.Message}). Falling back to pip.");
            return pipProvider;
        }
    }

    private void PrependPythonHomeToPath(string pythonHome)
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
        logger.ZLogInformation($"Prepended to PATH: {string.Join("; ", toAdd)}");
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
        bridge.SetupBuiltins(builtins, GlobalScope);
        builtins.__log_func__ = logFunction.ToPython();

        GlobalScope.Exec(PythonEmbedded.SetupScript);
    }

    /// <summary>
    /// Plant 3D and similar hosts load <c>python3xx.dll</c> before add-ins run.
    /// Initializing Pixi/pythonnet in the same process causes an uncatchable CLR crash.
    /// </summary>
    private static bool IsHostPythonRuntimeLoaded()
    {
        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
        {
            if (module.ModuleName.StartsWith("python", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
