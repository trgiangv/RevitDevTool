using DevTools.Execution.Diagnostics;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PythonDebuggerUninitializedTests
{
    [TestMethod]
    public void IsConnected_WhenPythonNotInitialized_ReturnsFalse()
    {
        if (PythonEngine.IsInitialized)
            Assert.Inconclusive("Python is already initialized in this process.");

        Assert.IsFalse(PythonDebugger.IsConnected(NullLogger.Instance));
    }
}

[TestClass]
public sealed class PythonDebuggerListeningTests
{
    [TestMethod]
    public async Task StartListening_WithGil_DoesNotThrow()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        using (Py.GIL())
        {
            PythonDebugger.StartListening(new DebugEndpoint(), NullLogger.Instance);
        }

        Assert.IsFalse(PythonDebugger.IsConnected(NullLogger.Instance));
    }
}

[TestClass]
public sealed class PythonDebuggerTests
{
    [TestMethod]
    public async Task IsConnected_WhenNoClientAttached_ReturnsFalse()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();

        Assert.IsFalse(PythonDebugger.IsConnected(NullLogger.Instance));
    }
}
