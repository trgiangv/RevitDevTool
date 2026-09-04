using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Providers.IronPython;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class ScriptExecutionStrategyFactoryTests
{
    [Fact]
    public void Create_ReturnsStrategyForEachMode()
    {
        using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var factory = provider.GetRequiredService<IScriptExecutionStrategyFactory>();
        var root = ExecutionTestHelpers.CreateTempDirectory("strategy-factory");

        try
        {
            Assert.IsType<PythonExecutionStrategy>(factory.Create(ExecutionMode.Python, Path.Combine(root, "a_script.py"), root));
            Assert.IsType<IronPythonExecutionStrategy>(factory.Create(ExecutionMode.IronPython, Path.Combine(root, "b_ipy_script.py"), root));
            Assert.IsType<CSharpExecutionStrategy>(factory.Create(ExecutionMode.CSharp, Path.Combine(root, "c_script.csx"), root));
            Assert.IsType<FSharpExecutionStrategy>(factory.Create(ExecutionMode.FSharp, Path.Combine(root, "d_script.fsx"), root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Create_UnsupportedMode_Throws()
    {
        using var provider = ExecutionTestHelpers.BuildExecutionServiceProvider();
        var factory = provider.GetRequiredService<IScriptExecutionStrategyFactory>();

        Assert.Throws<NotSupportedException>(() => factory.Create(ExecutionMode.Dotnet, "x.dll", "x"));
    }
}
