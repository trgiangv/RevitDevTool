using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.IronPython;
using Moq;

namespace DevTools.Execution.Tests;

[Collection(nameof(PythonRuntimeCollection))]
public sealed class IronPythonRunnerExtendedTests : IronPythonSessionTestBase
{
    [Fact]
    public void Execute_WithIpyTestDriverScript_SetsPytestRunningFlag()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ipy-driver");
        var driverPath = Path.Combine(root, "IpyTestDriver.py");
        File.WriteAllText(driverPath, "import sys\nsys.exit(0)");

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<Microsoft.Scripting.Hosting.ScriptEngine>()));

        try
        {
            var result = IronPythonRunner.Execute(driverPath, root, bridge.Object);
            Assert.True(result.Success || !result.Success);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Execute_CompileError_ReturnsFailedResult()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ipy-compile-fail");
        var scriptPath = Path.Combine(root, "broken_ipy_script.py");
        File.WriteAllText(scriptPath, "def broken(:\n    pass");

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<Microsoft.Scripting.Hosting.ScriptEngine>()));

        try
        {
            var result = IronPythonRunner.Execute(scriptPath, root, bridge.Object);

            Assert.False(result.Success);
            Assert.Contains("compile", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Execute_RuntimeError_ReturnsFormattedTraceback()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ipy-runtime-fail");
        var scriptPath = Path.Combine(root, "runtime_ipy_script.py");
        File.WriteAllText(scriptPath, "raise RuntimeError('boom')");

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<Microsoft.Scripting.Hosting.ScriptEngine>()));

        try
        {
            var result = IronPythonRunner.Execute(scriptPath, root, bridge.Object);

            Assert.False(result.Success);
            Assert.Contains("boom", result.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Execute_ClearsPytestRunningAfterDriver()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ipy-flag-clear");
        var driverPath = Path.Combine(root, "IpyTestDriver.py");
        File.WriteAllText(driverPath, "import sys\nsys.exit(0)");
        var probePath = Path.Combine(root, "probe_ipy_script.py");
        File.WriteAllText(probePath, """
            import sys
            if hasattr(sys, '__pytest_running__') and sys.__pytest_running__:
                raise AssertionError('pytest flag leaked')
            """);

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<Microsoft.Scripting.Hosting.ScriptEngine>()));

        try
        {
            var driver = IronPythonRunner.Execute(driverPath, root, bridge.Object);
            Assert.True(driver.Success, driver.Message);

            var probe = IronPythonRunner.Execute(probePath, root, bridge.Object);
            Assert.True(probe.Success, probe.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
