using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class UvEnvironmentProviderExtendedTests
{
    [Fact]
    public void IsVenvRunnable_ReturnsFalse_WhenPythonExeMissing()
    {
        var dir = ExecutionTestHelpers.CreateTempDirectory("uv-not-runnable");
        try
        {
            Assert.False(UvEnvironmentProvider.IsVenvRunnable(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void IsVenvRunnable_ReturnsFalse_WhenPyvenvHomeMissing()
    {
        var venv = Directory.CreateTempSubdirectory("uv-venv-");
        try
        {
            var scripts = Path.Combine(venv.FullName, "Scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "python.exe"), string.Empty);
            File.WriteAllText(Path.Combine(venv.FullName, "pyvenv.cfg"), "home = C:\\missing\\python\n");

            Assert.False(UvEnvironmentProvider.IsVenvRunnable(venv.FullName));
        }
        finally
        {
            venv.Delete(recursive: true);
        }
    }

    [Fact]
    public void AttachHostInterpreter_DoesNotReprobeWhenAlreadyBound()
    {
        var probeCount = 0;
        var provider = new UvEnvironmentProvider(
            NullLogger<UvEnvironmentProvider>.Instance,
            () =>
            {
                probeCount++;
                return "3.13";
            });

        _ = provider.BoundPythonVersion;
        provider.AttachHostInterpreter("C:\\fake\\python313.dll");
        _ = provider.BoundPythonVersion;

        Assert.Equal(1, probeCount);
    }

    [Fact]
    public void UvArgs_BuildExpectedCommandLines()
    {
        Assert.Equal(["python", "install", "--no-bin", "3.13"], UvEnvironmentProvider.UvArgs.PythonInstall("3.13"));
        Assert.Equal(["venv", "--clear", "--python", "3.13", @"C:\env"], UvEnvironmentProvider.UvArgs.Venv("3.13", @"C:\env"));
        Assert.Equal(
            ["pip", "list", "--python", @"C:\env\Scripts\python.exe", "--format=json"],
            UvEnvironmentProvider.UvArgs.PipListJson(@"C:\env\Scripts\python.exe"));
        Assert.Equal(
            ["pip", "uninstall", "--python", @"C:\env\Scripts\python.exe", "-y", "requests"],
            UvEnvironmentProvider.UvArgs.PipUninstall(@"C:\env\Scripts\python.exe", "requests"));
        Assert.Equal(
            ["pip", "install", "--python", @"C:\env\Scripts\python.exe", "requests", "packaging"],
            UvEnvironmentProvider.UvArgs.PipInstall(@"C:\env\Scripts\python.exe", ["requests", "packaging"]));
    }

    [Fact]
    public void GetPythonDllPath_ThrowsWhenHomeMissing()
    {
        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => "3.13");
        Assert.Throws<DirectoryNotFoundException>(() => _ = provider.GetPythonDllPath());
    }
}
