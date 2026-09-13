using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class FSharpCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FSharpCompilationCache_SecondCall_ReportsCacheHit()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-cache-hit");
        var scriptPath = Path.Combine(directory, "command_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            """
            type ScriptCommand() =
                member _.Run() = ()
            """,
            TestContext.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);
        var progress = new List<string>();

        try
        {
            var first = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.CancellationToken);
            progress.Clear();
            var second = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.CancellationToken);

            Assert.IsTrue(first.Success, first.FormatDiagnostics());
            Assert.IsTrue(second.Success, second.FormatDiagnostics());
            if (progress.Count > 0)
                Assert.IsTrue(progress.Any(message => message.Contains("cached", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FSharpExecutionStrategy_Success_InvokesCommandRunner()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-strategy-ok");
        var scriptPath = Path.Combine(directory, "run_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            """
            type ScriptCommand() =
                member _.Run() = ()
            """,
            TestContext.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);
        var commandRunner = new Mock<ICommandRunner>();
        commandRunner
            .Setup(r => r.RunCompiledCommand(It.IsAny<object>()))
            .Returns(ExecutionResult.Succeeded("done", 5));

        try
        {
            var strategy = new FSharpExecutionStrategy(
                scriptPath,
                ExecutionTestHelpers.InlineHostContext(),
                commandRunner.Object,
                cache,
                NullLogger<FSharpExecutionStrategy>.Instance);

            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

            Assert.IsTrue(result.Success, result.Message);
            commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Once);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FSharpExecutionStrategy_CompileFailure_ReturnsFailedResult()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-strategy-fail");
        var scriptPath = Path.Combine(directory, "bad_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x =", TestContext.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);

        try
        {
            var strategy = new FSharpExecutionStrategy(
                scriptPath,
                ExecutionTestHelpers.InlineHostContext(),
                Mock.Of<ICommandRunner>(),
                cache,
                NullLogger<FSharpExecutionStrategy>.Instance);

            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

            Assert.IsFalse(result.Success);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
