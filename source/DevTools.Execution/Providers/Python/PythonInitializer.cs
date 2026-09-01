using System.Diagnostics;
using System.IO;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Python.Runtime;
using ZLogger;

namespace DevTools.Execution.Providers.Python;

public sealed class PythonInitializer(
    [FromKeyedServices(PythonBackend.Pixi)] PyEnvironmentProvider pixiProvider,
    [FromKeyedServices(PythonBackend.Uv)] PyEnvironmentProvider uvProvider,
    [FromKeyedServices(PythonBackend.Pip)] PyEnvironmentProvider pipProvider,
    IPythonBridge bridge,
    ILogger<PythonInitializer> logger)
{
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public PyModule? GlobalScope { get; private set; }
    public PyEnvironmentProvider? Provider { get; private set; }
    public bool HostOwnsInterpreter { get; private set; }

    public bool IsInitialized => PythonEngine.IsInitialized
                                 && Provider?.IsEnvironmentReady() == true;

    public async Task InitializeAsync()
    {
        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsInitialized) return;

            var hostDll = PythonNativeEnvironment.TryGetHostPythonDll(out var d) ? d : null;
            HostOwnsInterpreter = hostDll is not null;

            if (hostDll is not null)
            {
                uvProvider.AttachHostInterpreter(hostDll);
                pipProvider.AttachHostInterpreter(hostDll);
                InitializeEngine(hostDll, null);
            }

            Provider = await ResolveProviderAsync().ConfigureAwait(false);

            if (hostDll is null)
                InitializeEngine(null, Provider);

            logger.ZLogInformation($"Python init: hostOwnsInterpreter={HostOwnsInterpreter} backend={Provider.Backend}");
            SetupRuntime();
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

            if (HostOwnsInterpreter)
                return;

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

    private async Task<PyEnvironmentProvider> ResolveProviderAsync()
    {
        var primary = HostOwnsInterpreter ? uvProvider : pixiProvider;
        try
        {
            await primary.SetupEnvironmentAsync().ConfigureAwait(false);
            return primary;
        }
        catch (Exception ex)
        {
            logger.ZLogWarning($"{primary.Backend} unavailable ({ex.GetType().Name}: {ex.Message}). Falling back to pip.");
            await pipProvider.SetupEnvironmentAsync().ConfigureAwait(false);
            return pipProvider;
        }
    }

    private void InitializeEngine(string? hostPythonDll, PyEnvironmentProvider? provider)
    {
        if (PythonEngine.IsInitialized) return;

        if (hostPythonDll is not null)
        {
            logger.ZLogWarning($"Host embedded Python already loaded ({hostPythonDll}). Attaching pythonnet; the package sidecar does not replace this interpreter.");
            Runtime.PythonDLL = hostPythonDll;
            PythonNativeEnvironment.ClearPythonnetStash(hostPythonDll);
        }
        else
        {
            var home = (provider ?? throw new InvalidOperationException("Python environment provider not initialized.")).PythonHome;
            PythonNativeEnvironment.PrepareProcess(home, logger);
            Runtime.PythonDLL = provider.GetPythonDllPath();
            PythonEngine.PythonHome = home;
        }

        PythonEngine.ProgramName = bridge.ProgramName;
        PythonEngine.Initialize();
        PythonEngine.BeginAllowThreads();
    }

    private void SetupRuntime()
    {
        using (Py.GIL())
        {
            if (!HostOwnsInterpreter && Provider?.PythonHome is { Length: > 0 } home)
                PythonNativeEnvironment.AddPythonDllDirectories(home);

            if (HostOwnsInterpreter)
            {
                PythonDepsManager.TryResolveSidecarStdlib(Provider, out var lib, out var dlls);
                logger.ZLogInformation(
                    $"Python sidecar overlay site={Provider?.SitePackagesDir} lib={lib} dlls={(dlls.Length > 0 ? dlls : "(none)")}");
                if (lib.Length > 0)
                    PythonNativeEnvironment.LoadStableAbiForwarder(Path.GetDirectoryName(lib) ?? string.Empty, logger);
            }

            var probe = PythonDepsManager.InjectSitePackages(this);
            if (probe.Length > 0)
                logger.ZLogInformation($"Python sidecar overlay probe={probe}");
            SetupGlobalScope();
            PythonDebugger.StartListening(logger);
        }
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
}
