using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;
using Python.Runtime;

namespace DevTools.Execution.Tests;

public sealed class PythonDebuggerUninitializedTests
{
    [Fact]
    public void IsConnected_WhenPythonNotInitialized_ReturnsFalse()
    {
        if (PythonEngine.IsInitialized)
            Assert.Skip("Python is already initialized in this process.");

        Assert.False(PythonDebugger.IsConnected(NullLogger.Instance));
    }
}

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PythonDebuggerListeningTests
{
    [Fact]
    public async Task StartListening_WithGil_DoesNotThrow()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        using (Python.Runtime.Py.GIL())
        {
            PythonDebugger.StartListening(NullLogger.Instance);
        }

        Assert.False(PythonDebugger.IsConnected(NullLogger.Instance));
    }
}

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PythonDebuggerTests
{
    [Fact]
    public async Task IsConnected_WhenNoClientAttached_ReturnsFalse()
    {
        await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();

        Assert.False(PythonDebugger.IsConnected(NullLogger.Instance));
    }
}
