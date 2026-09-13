using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.IronPython;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Scripting.Hosting;
using Moq;

namespace DevTools.Execution.Tests;

/// <summary>
/// Session-engine IronPython tests shut down the static engine after each method.
/// </summary>
public abstract class IronPythonSessionTestBase
{
    [TestCleanup]
    public void SessionCleanup() => new IronPythonDebugger().Shutdown();
}

[TestClass]
public sealed class IronPythonExecutionTests : IronPythonSessionTestBase
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
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

    [TestMethod]
    public void IronPythonRunner_IsIpyTestDriverScript_DetectsDriverName()
    {
        Assert.IsTrue(IronPythonRunner.IsIpyTestDriverScript(@"C:\scripts\IpyTestDriver.py"));
        Assert.IsFalse(IronPythonRunner.IsIpyTestDriverScript(@"C:\scripts\other_ipy_script.py"));
    }

    [TestMethod]
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

            Assert.IsTrue(result.Success);
            bridge.Verify(b => b.ConfigureEngine(It.IsAny<ScriptEngine>()), Times.AtMostOnce);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void IronPythonRunner_CoFilename_IsCanonicalPath()
    {
        var root = ExecutionTestHelpers.CreateTempDirectory("ironpython-cofilename");
        var scriptPath = Path.Combine(root, "name_ipy_script.py");
        var markerPath = Path.Combine(root, "co.txt");
        File.WriteAllText(
            scriptPath,
            "import sys\nwith open(r'" + markerPath.Replace("\\", "\\\\") + "', 'w') as f:\n    f.write(sys._getframe().f_code.co_filename)\n");

        var bridge = new Mock<IIronPythonBridge>();
        bridge.Setup(b => b.ConfigureEngine(It.IsAny<ScriptEngine>()));

        try
        {
            var result = IronPythonRunner.Execute(scriptPath, root, bridge.Object);
            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(File.Exists(markerPath), result.Message);
            Assert.AreEqual(Path.GetFullPath(scriptPath), File.ReadAllText(markerPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
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
            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

            Assert.IsTrue(result.Success);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
