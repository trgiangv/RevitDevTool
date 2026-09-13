using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class UvEnvironmentProviderExtendedTests
{
    [TestMethod]
    public void IsVenvRunnable_ReturnsFalse_WhenPythonExeMissing()
    {
        var dir = ExecutionTestHelpers.CreateTempDirectory("uv-not-runnable");
        try
        {
            Assert.IsFalse(UvEnvironmentProvider.IsVenvRunnable(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [TestMethod]
    public void IsVenvRunnable_ReturnsFalse_WhenPyvenvHomeMissing()
    {
        var venv = Directory.CreateTempSubdirectory("uv-venv-");
        try
        {
            var scripts = Path.Combine(venv.FullName, "Scripts");
            Directory.CreateDirectory(scripts);
            File.WriteAllText(Path.Combine(scripts, "python.exe"), string.Empty);
            File.WriteAllText(Path.Combine(venv.FullName, "pyvenv.cfg"), "home = C:\\missing\\python\n");

            Assert.IsFalse(UvEnvironmentProvider.IsVenvRunnable(venv.FullName));
        }
        finally
        {
            venv.Delete(recursive: true);
        }
    }

    [TestMethod]
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

        Assert.AreEqual(1, probeCount);
    }

    [TestMethod]
    public void UvArgs_BuildExpectedCommandLines()
    {
        Assert.AreSequenceEqual(["python", "install", "--no-bin", "3.13"], UvEnvironmentProvider.UvArgs.PythonInstall("3.13"));
        Assert.AreSequenceEqual(["venv", "--clear", "--python", "3.13", @"C:\env"], UvEnvironmentProvider.UvArgs.Venv("3.13", @"C:\env"));
        Assert.AreSequenceEqual(
            ["pip", "list", "--python", @"C:\env\Scripts\python.exe", "--format=json"],
            UvEnvironmentProvider.UvArgs.PipListJson(@"C:\env\Scripts\python.exe"));
        Assert.AreSequenceEqual(
            ["pip", "uninstall", "--python", @"C:\env\Scripts\python.exe", "-y", "requests"],
            UvEnvironmentProvider.UvArgs.PipUninstall(@"C:\env\Scripts\python.exe", "requests"));
        Assert.AreSequenceEqual(
            ["pip", "install", "--python", @"C:\env\Scripts\python.exe", "requests", "packaging"],
            UvEnvironmentProvider.UvArgs.PipInstall(@"C:\env\Scripts\python.exe", ["requests", "packaging"]));
    }

    [TestMethod]
    public void GetPythonDllPath_ThrowsWhenHomeMissing()
    {
        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => "3.13");
        Assert.ThrowsExactly<DirectoryNotFoundException>(() => _ = provider.GetPythonDllPath());
    }
}
