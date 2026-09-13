using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class UvArgsTests
{
    [TestMethod]
    public void PipInstall_IsUvPipNotPythonDashM()
    {
        var args = UvEnvironmentProvider.UvArgs.PipInstall(@"C:\venv\Scripts\python.exe", ["mcp>=2", "pytest"]);

        Assert.AreSequenceEqual(["pip", "install", "--python", @"C:\venv\Scripts\python.exe", "mcp>=2", "pytest"], args);
        Assert.DoesNotContain("-m", args);
    }

    [TestMethod]
    public void PipInstall_Upgrade_InsertsFlagBeforePython()
    {
        var args = UvEnvironmentProvider.UvArgs.PipInstall(@"C:\venv\Scripts\python.exe", ["packaging"], upgrade: true);

        Assert.AreSequenceEqual(
            ["pip", "install", "--upgrade", "--python", @"C:\venv\Scripts\python.exe", "packaging"],
            args);
    }

    [TestMethod]
    public void PipListJson_AndUninstall_SharePythonSelector()
    {
        const string exe = @"C:\venv\Scripts\python.exe";

        Assert.AreSequenceEqual(["pip", "list", "--python", exe, "--format=json"], UvEnvironmentProvider.UvArgs.PipListJson(exe));
        Assert.AreSequenceEqual(["pip", "uninstall", "--python", exe, "-y", "debugpy"], UvEnvironmentProvider.UvArgs.PipUninstall(exe, "debugpy"));
        Assert.AreSequenceEqual(["python", "install", "--no-bin", "3.13"], UvEnvironmentProvider.UvArgs.PythonInstall("3.13"));
        Assert.AreSequenceEqual(["venv", "--clear", "--python", "3.13", @"C:\venv"], UvEnvironmentProvider.UvArgs.Venv("3.13", @"C:\venv"));
    }
}
