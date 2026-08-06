using DevTools.Execution.Providers.Python;
using DevTools.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

/// <summary>
/// Opt-in smoke: .NET provider → CliWrap pixi → Python.NET (AppData env).
/// Skipped in default CI; host SetupRevit/SetupAcad not run (need host assemblies).
/// </summary>
public sealed class PixiEnvironmentSmokeTests
{
    private static readonly Lock InitLock = new();
    private static bool _pythonNetInitialized;

    [Fact]
    public async Task SetupEnvironment_ThenPythonNetCanImportSys()
    {
        // $env:RUN_PIXI_SMOKE=1; dotnet test ... --filter PixiEnvironmentSmokeTests
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_PIXI_SMOKE"), "1", StringComparison.Ordinal))
        {
            Assert.Skip("Set RUN_PIXI_SMOKE=1 to run AppData Pixi + Python.NET smoke.");
        }

        PythonEmbedded.Configure(HostApp.Revit);
        var provider = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);

        await PythonInstaller.SetupPixiAsync(NullLogger.Instance);
        Assert.True(PythonInstaller.IsPixiInstalled(), "pixi.exe missing under AppData/bin");

        if (!provider.IsEnvironmentReady())
            await provider.SetupEnvironmentAsync();

        Assert.True(provider.IsEnvironmentReady(), $"python.exe missing at {provider.PythonExe}");

        await provider.SetupEnvironmentAsync();
        Assert.True(provider.IsEnvironmentReady());

        InitializePythonNet(provider);
        Assert.True(PythonEngine.IsInitialized);

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Exec("import sys; assert sys.version_info >= (3, 11)");
        }
    }

    private static void InitializePythonNet(PyEnvironmentProvider provider)
    {
        lock (InitLock)
        {
            if (_pythonNetInitialized || PythonEngine.IsInitialized)
            {
                _pythonNetInitialized = true;
                return;
            }

            var pythonHome = provider.PythonHome;
            var libraryBin = Path.Combine(pythonHome, "Library", "bin");
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            var toAdd = new[] { pythonHome, libraryBin }
                .Where(Directory.Exists)
                .Where(d => currentPath.IndexOf(d, StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();
            if (toAdd.Count > 0)
                Environment.SetEnvironmentVariable("PATH", string.Join(";", toAdd) + ";" + currentPath);

            Runtime.PythonDLL = provider.GetPythonDllPath();
            PythonEngine.PythonHome = pythonHome;
            PythonEngine.ProgramName = "DevTools.Execution.Tests";
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            _pythonNetInitialized = true;
        }
    }
}
