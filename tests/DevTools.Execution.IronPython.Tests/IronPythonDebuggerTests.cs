using DevTools.Execution.Interfaces;
using DevTools.Execution.Providers.IronPython;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class IronPythonDebuggerTests : IronPythonSessionTestBase
{
    [TestMethod]
    public void StartListening_WithoutZip_DoesNotThrow_IsAttachedFalse()
    {
        var debugger = new IronPythonDebugger();
        debugger.StartListening();

        Assert.IsFalse(debugger.IsAttached);
    }

    [TestMethod]
    public void SessionEngine_HasGetframe()
    {
        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        var scope = engine.CreateScope();
        engine.CreateScriptSourceFromString(
            "import sys\n__has_getframe__ = hasattr(sys, '_getframe')\n__platform__ = sys.platform").Execute(scope);

        Assert.IsTrue(scope.GetVariable<bool>("__has_getframe__"));
        Assert.AreNotEqual("cli", scope.GetVariable<string>("__platform__"));
    }

    [TestMethod]
    public void ImportPydevd_WhenExtractPresent_ReportsIronPython()
    {
        if (!PydevdInstaller.IsInstalled())
            Assert.Inconclusive("pydevd 2.8.0 extract is not on disk.");

        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        var scope = engine.CreateScope();
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
            """).Execute(scope);

        Assert.IsTrue(scope.GetVariable<bool>("__is_ipy__"));
        Assert.IsTrue(scope.GetVariable<bool>("__is_windows__"));
        Assert.AreEqual("WINDOWS", scope.GetVariable<string>("__ide_os__"));
        Assert.IsFalse(scope.GetVariable<bool>("__cython__"));
        Assert.IsFalse(debugger.IsAttached);
    }

    [TestMethod]
    public void SetTrace_Null_DoesNotThrowOnLocalEngine()
    {
        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        DlrScriptHost.SetTrace(engine, null);
    }

    [TestMethod]
    public void EnsureCurrentThreadTraced_AfterListen_DoesNotThrow()
    {
        if (!PydevdInstaller.IsInstalled())
            Assert.Inconclusive("pydevd 2.8.0 extract is not on disk.");

        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        debugger.StartListening();
        debugger.EnsureCurrentThreadTraced();

        var scope = DlrScriptHost.CreateScope(engine);
        DlrScriptHost.Execute(engine, "import sys\n__traced__ = sys.gettrace() is not None", scope);
        Assert.IsTrue(DlrScriptHost.GetVariable<bool>(scope, "__traced__"));
        Assert.IsFalse(debugger.IsAttached);
    }

    [TestMethod]
    public void DlrScriptHost_Execute_OnLocalEngine_SetsVariables()
    {
        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        var scope = DlrScriptHost.CreateScope(engine);
        DlrScriptHost.Execute(engine, "value = 41 + 1", scope);

        Assert.AreEqual(42, Convert.ToInt32(DlrScriptHost.GetVariable<object>(scope, "value")));
    }

    [TestMethod]
    public void FindInstanceMethod_ResolvesExecuteWithoutAmbiguousMatch()
    {
        var debugger = new IronPythonDebugger();
        var engine = debugger.GetOrCreateEngine(Mock.Of<IIronPythonBridge>());
        var scope = engine.CreateScope();
        var source = engine.CreateScriptSourceFromString("pass");

        var execute = DlrScriptHost.FindInstanceMethod(source.GetType(), DlrScriptHost.ExecuteName, [scope.GetType()]);
        Assert.IsFalse(execute.IsGenericMethod);
        Assert.HasCount(1, execute.GetParameters());

        var createScope = DlrScriptHost.FindInstanceMethod(engine.GetType(), DlrScriptHost.CreateScopeName, Type.EmptyTypes);
        Assert.IsEmpty(createScope.GetParameters());

        var fromString = DlrScriptHost.FindInstanceMethod(
            engine.GetType(),
            DlrScriptHost.CreateScriptSourceFromStringName,
            [typeof(string)]);
        Assert.HasCount(1, fromString.GetParameters());

        var getVariable = DlrScriptHost.FindGenericInstanceMethod(
            scope.GetType(),
            DlrScriptHost.GetVariableName,
            genericArity: 1,
            [typeof(string)]);
        Assert.IsNotNull(getVariable);
    }
}
