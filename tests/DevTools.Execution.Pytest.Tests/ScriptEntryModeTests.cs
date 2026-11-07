using DevTools.Execution.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class ScriptEntryModeTests
{
    [TestMethod]
    [DataRow(@"C:\s\tool_script.py", ExecutionMode.Python)]
    [DataRow(@"C:\s\tool_ipy_script.py", ExecutionMode.IronPython)]
    [DataRow(@"C:\s\TOOL_IPY_SCRIPT.PY", ExecutionMode.IronPython)]
    [DataRow(@"C:\s\tool_script.fsx", ExecutionMode.FSharp)]
    [DataRow(@"C:\s\tool_script.csx", ExecutionMode.CSharp)]
    [DataRow(@"C:\s\helper.py", ExecutionMode.Python)]
    public void TryResolve_ScriptExtension_ReturnsMode(string path, ExecutionMode expected)
    {
        Assert.IsTrue(ScriptEntryMode.TryResolve(path, out var mode));
        Assert.AreEqual(expected, mode);
    }

    [TestMethod]
    [DataRow(@"C:\s\Commands.dll")]
    [DataRow(@"C:\s\notes.txt")]
    [DataRow(@"C:\s\noext")]
    public void TryResolve_OtherExtension_ReturnsFalse(string path)
    {
        Assert.IsFalse(ScriptEntryMode.TryResolve(path, out var mode));
        Assert.AreEqual(ExecutionMode.Unsupported, mode);
    }

    [TestMethod]
    [DataRow(@"C:\s\tool_script.py", true)]
    [DataRow(@"C:\s\tool_ipy_script.py", true)]
    [DataRow(@"C:\s\Tool_Script.FSX", true)]
    [DataRow(@"C:\s\tool_script.csx", true)]
    [DataRow(@"C:\s\helper.py", false)]
    [DataRow(@"C:\script\helper.py", false)]
    [DataRow(@"C:\s\Commands.dll", false)]
    public void IsEntryScript_MatchesDiscoveryPatterns(string path, bool expected) =>
        Assert.AreEqual(expected, ScriptEntryMode.IsEntryScript(path));
}
