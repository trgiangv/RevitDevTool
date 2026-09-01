using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

public sealed class UvArgsTests
{
    [Fact]
    public void PipInstall_IsUvPipNotPythonDashM()
    {
        var args = UvEnvironmentProvider.UvArgs.PipInstall(@"C:\venv\Scripts\python.exe", ["mcp>=2", "pytest"]);

        Assert.Equal(["pip", "install", "--python", @"C:\venv\Scripts\python.exe", "mcp>=2", "pytest"], args);
        Assert.DoesNotContain("-m", args);
    }

    [Fact]
    public void PipInstall_Upgrade_InsertsFlagBeforePython()
    {
        var args = UvEnvironmentProvider.UvArgs.PipInstall(@"C:\venv\Scripts\python.exe", ["packaging"], upgrade: true);

        Assert.Equal(
            ["pip", "install", "--upgrade", "--python", @"C:\venv\Scripts\python.exe", "packaging"],
            args);
    }

    [Fact]
    public void PipListJson_AndUninstall_SharePythonSelector()
    {
        const string exe = @"C:\venv\Scripts\python.exe";

        Assert.Equal(["pip", "list", "--python", exe, "--format=json"], UvEnvironmentProvider.UvArgs.PipListJson(exe));
        Assert.Equal(["pip", "uninstall", "--python", exe, "-y", "debugpy"], UvEnvironmentProvider.UvArgs.PipUninstall(exe, "debugpy"));
        Assert.Equal(["python", "install", "--no-bin", "3.13"], UvEnvironmentProvider.UvArgs.PythonInstall("3.13"));
        Assert.Equal(["venv", "--clear", "--python", "3.13", @"C:\venv"], UvEnvironmentProvider.UvArgs.Venv("3.13", @"C:\venv"));
    }
}
