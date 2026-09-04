using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.IronPython;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Scripting.Hosting;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class IronPythonExecutionTests
{
    [Fact]
    public void IronPythonSearchPaths_IncludesScriptAndLibDirectories()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ironpython-paths");
        var libDir = Path.Combine(root, "lib");
        Directory.CreateDirectory(libDir);
        var scriptsDir = Path.Combine(root, "scripts");
        Directory.CreateDirectory(scriptsDir);
        var scriptPath = Path.Combine(scriptsDir, "sample_ipy_script.py");
        File.WriteAllText(scriptPath, "x = 1");

        try
        {
            var paths = IronPythonSearchPaths.ForNativeHost(scriptPath, root);

            Assert.Contains(scriptsDir, paths, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(root, paths, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void IronPythonRunner_IsIpyTestDriverScript_DetectsDriverName()
    {
        Assert.True(IronPythonRunner.IsIpyTestDriverScript(@"C:\scripts\IpyTestDriver.py"));
        Assert.False(IronPythonRunner.IsIpyTestDriverScript(@"C:\scripts\other_ipy_script.py"));
    }

    [Fact]
    public void IronPythonRunner_ExecutesSimpleScript()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ironpython-run");
        var scriptPath = Path.Combine(root, "hello_ipy_script.py");
        File.WriteAllText(scriptPath, "result = 41 + 1");

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<ScriptEngine>()));

        try
        {
            var result = IronPythonRunner.Execute(scriptPath, root, bridge.Object);

            Assert.True(result.Success);
            bridge.Verify(b => b.ConfigureEngine(It.IsAny<ScriptEngine>()), Times.Once);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task IronPythonExecutionStrategy_ReportsSuccess()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ironpython-strategy");
        var scriptPath = Path.Combine(root, "run_ipy_script.py");
        File.WriteAllText(scriptPath, "value = 1");

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<ScriptEngine>()));
        var strategy = new IronPythonExecutionStrategy(
            scriptPath,
            root,
            bridge.Object,
            ExecutionTestHelpers.InlineHostContext(),
            NullLogger<IronPythonExecutionStrategy>.Instance);

        try
        {
            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.True(result.Success);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
