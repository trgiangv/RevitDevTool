using DevTools.Execution.Providers.IronPython;
using IronPython.Hosting;
using Ipy = IronPython.Hosting.Python;
using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Tests;

public sealed class IronPythonPytestRunningTests
{
    [Fact]
    public void IsIpyTestDriverScript_MatchesDriverFilenameOnly()
    {
        Assert.True(IronPythonRunner.IsIpyTestDriverScript(@"C:\pixi-env\IpyTestDriver.py"));
        Assert.False(IronPythonRunner.IsIpyTestDriverScript(@"C:\tests\test_foo_ipy_script.py"));
    }

    [Fact]
    public void SetupRevit_SkipsPrintHijackWhenPytestRunning()
    {
        var setupPath = Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.Execution",
            "Resources",
            "scripts",
            "SetupRevit.py");
        Assert.True(File.Exists(setupPath), $"SetupRevit.py not found at '{setupPath}'.");
        var setup = File.ReadAllText(setupPath);

        var engineWithFlag = CreateEngine(pytestRunning: true);
        engineWithFlag.CreateScriptSourceFromString(setup, "SetupRevit.py").Execute(engineWithFlag.CreateScope());
        dynamic builtinsWithFlag = engineWithFlag.GetBuiltinModule();
        Assert.NotEqual("custom_print", builtinsWithFlag.print.__name__);

        var engineWithoutFlag = CreateEngine(pytestRunning: false);
        engineWithoutFlag.CreateScriptSourceFromString(setup, "SetupRevit.py").Execute(engineWithoutFlag.CreateScope());
        dynamic builtinsWithoutFlag = engineWithoutFlag.GetBuiltinModule();
        Assert.Equal("custom_print", builtinsWithoutFlag.print.__name__);
    }

    private static ScriptEngine CreateEngine(bool pytestRunning)
    {
        var engine = Ipy.CreateEngine();
        engine.GetBuiltinModule().SetVariable("__log_func__", new Action<object>(_ => { }));
        if (pytestRunning)
        {
            dynamic sys = engine.GetSysModule();
            sys.__pytest_running__ = true;
        }

        return engine;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "RevitDevTool.slnx")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the RevitDevTool repository root.");
    }
}
