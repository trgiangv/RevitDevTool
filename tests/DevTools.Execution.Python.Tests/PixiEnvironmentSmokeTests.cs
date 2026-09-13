using System.Text;
using CliWrap;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

/// <summary>
/// Pixi AppData env + Python.NET (no Revit/AutoCAD process).
/// Downloads pixi via <see cref="PixiInstaller.SetupPixiAsync"/> when missing.
/// </summary>
[TestClass]
public sealed class PixiEnvironmentSmokeTests
{
    public TestContext TestContext { get; set; } = null!;

    private static readonly Lock InitLock = new();
    private static bool _pythonNetInitialized;

    [TestMethod]
    public async Task SetupEnvironment_ThenPythonNetCanImportSys()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        var provider = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);

        await PixiInstaller.SetupPixiAsync(NullLogger.Instance);
        Assert.IsTrue(PixiInstaller.IsPixiInstalled(), "pixi.exe missing under AppData/bin");

        if (!provider.IsEnvironmentReady())
            await provider.SetupEnvironmentAsync();

        Assert.IsTrue(provider.IsEnvironmentReady(), $"python.exe missing at {provider.PythonExe}");

        await provider.SetupEnvironmentAsync();
        Assert.IsTrue(provider.IsEnvironmentReady());

        InitializePythonNet(provider);
        Assert.IsTrue(PythonEngine.IsInitialized);

        using (Py.GIL())
        {
            using var scope = Py.CreateScope();
            scope.Exec("import sys; assert sys.version_info >= (3, 11)");
        }
    }

    [TestMethod]
    public async Task PixiCli_VersionAndHelp_PrintToStdout()
    {
        try
        {
            await PixiInstaller.SetupPixiAsync(NullLogger.Instance);
        }
        catch (Exception ex)
        {
            Assert.Inconclusive($"Pixi download failed after retry: {ex.Message}");
        }

        var versionStdout = new StringBuilder();
        var version = await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments(["--version"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(versionStdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(TestContext.CancellationToken);
        Assert.AreEqual(0, version.ExitCode);
        Assert.Contains("pixi", versionStdout.ToString(), StringComparison.OrdinalIgnoreCase);

        var helpStdout = new StringBuilder();
        var help = await Cli.Wrap(PixiInstaller.PixiExePath)
            .WithArguments(["--help"])
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(helpStdout))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync(TestContext.CancellationToken);
        Assert.AreEqual(0, help.ExitCode);
        Assert.Contains("Usage", helpStdout.ToString(), StringComparison.OrdinalIgnoreCase);
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
            PythonNativeEnvironment.PrepareProcess(pythonHome);

            Runtime.PythonDLL = provider.GetPythonDllPath();
            PythonEngine.PythonHome = pythonHome;
            PythonEngine.ProgramName = "DevTools.Execution.Tests";
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            _pythonNetInitialized = true;
        }
    }
}
