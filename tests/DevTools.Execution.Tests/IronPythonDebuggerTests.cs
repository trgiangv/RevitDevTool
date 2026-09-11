using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.IronPython;
using Moq;

namespace DevTools.Execution.Tests;

[Collection(nameof(PythonRuntimeCollection))]
public sealed class IronPythonDebuggerTests : IronPythonSessionTestBase
{
    [Fact]
    public void StartListening_WithoutZip_DoesNotThrow_IsAttachedFalse()
    {
        var debugger = new IronPythonDebugger();
        var thrown = Record.Exception(() => debugger.StartListening());

        Assert.Null(thrown);
        Assert.False(debugger.IsAttached);
    }

    [Fact]
    public void SessionEngine_HasGetframe()
    {
        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        var scope = engine.CreateScope();
        engine.CreateScriptSourceFromString(
            "import sys\n__has_getframe__ = hasattr(sys, '_getframe')\n__platform__ = sys.platform").Execute(scope);

        Assert.True(scope.GetVariable<bool>("__has_getframe__"));
        Assert.NotEqual("cli", scope.GetVariable<string>("__platform__"));
    }

    [Fact]
    public void ImportPydevd_WhenExtractPresent_ReportsIronPython()
    {
        if (!PydevdInstaller.IsInstalled())
            Assert.Skip("pydevd 2.8.0 extract is not on disk.");

        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        var scope = engine.CreateScope();
        var thrown = Record.Exception(() =>
            engine.CreateScriptSourceFromString("""
                import sys
                __pydevd_orig_platform = sys.platform
                sys.platform = 'cli'
                from _pydevd_bundle import pydevd_constants
                pydevd_constants.IS_WINDOWS = True
                sys.platform = __pydevd_orig_platform
                import pydevd
                import pydevd_file_utils
                __is_ipy__ = pydevd_constants.IS_IRONPYTHON
                __is_windows__ = pydevd_constants.IS_WINDOWS
                __ide_os__ = pydevd_file_utils._ide_os
                __cython__ = pydevd_constants.CYTHON_SUPPORTED
                """).Execute(scope));

        Assert.Null(thrown);
        Assert.True(scope.GetVariable<bool>("__is_ipy__"));
        Assert.True(scope.GetVariable<bool>("__is_windows__"));
        Assert.Equal("WINDOWS", scope.GetVariable<string>("__ide_os__"));
        Assert.False(scope.GetVariable<bool>("__cython__"));
        Assert.False(debugger.IsAttached);
    }

    [Fact]
    public void EnsureCurrentThreadTraced_AfterListen_DoesNotThrow()
    {
        if (!PydevdInstaller.IsInstalled())
            Assert.Skip("pydevd 2.8.0 extract is not on disk.");

        var debugger = new IronPythonDebugger();
        debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        debugger.StartListening();

        var thrown = Record.Exception(debugger.EnsureCurrentThreadTraced);

        Assert.Null(thrown);
        // RevitIPyExecutionStrategy yields to this engine only while attached.
        Assert.False(debugger.IsAttached);
    }
}
