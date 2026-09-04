using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class CSharpExecutionStrategyTests
{
    [Fact]
    public async Task ExecuteAsync_ValidScript_RunsThroughHostContext()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-strategy");
        var scriptPath = Path.Combine(directory, "run_script.csx");
        await File.WriteAllTextAsync(
            scriptPath,
            "public sealed class ScriptCommand { public static int M() => 3; }",
            TestContext.Current.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var compiler = new CSharpCompiler(NullLogger<CSharpCompiler>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var cache = new CSharpCompilationCache(bridge, compiler, NullLogger<CSharpCompilationCache>.Instance);
        var commandRunner = new Mock<ICommandRunner>();
        commandRunner
            .Setup(r => r.RunCompiledCommand(It.IsAny<object>()))
            .Returns(ExecutionResult.Succeeded("done", 5));
        var progress = new List<string>();

        try
        {
            var strategy = new CSharpExecutionStrategy(
                scriptPath,
                ExecutionTestHelpers.InlineHostContext(),
                commandRunner.Object,
                cache,
                NullLogger<CSharpExecutionStrategy>.Instance);

            var result = await strategy.ExecuteAsync(new Progress<string>(message =>
            {
                lock (progress)
                    progress.Add(message);
            }), TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.Message);
            commandRunner.Verify(r => r.RunCompiledCommand(It.IsAny<object>()), Times.Once);
            string[] messages;
            lock (progress)
                messages = progress.ToArray();
            Assert.Contains(messages, message => message.Contains("run_script.csx", StringComparison.Ordinal));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CompilationFailure_ReturnsFailedResult()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-strategy-fail");
        var scriptPath = Path.Combine(directory, "bad_script.csx");
        await File.WriteAllTextAsync(scriptPath, "public class {", TestContext.Current.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var compiler = new CSharpCompiler(NullLogger<CSharpCompiler>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var cache = new CSharpCompilationCache(bridge, compiler, NullLogger<CSharpCompilationCache>.Instance);

        try
        {
            var strategy = new CSharpExecutionStrategy(
                scriptPath,
                ExecutionTestHelpers.InlineHostContext(),
                Mock.Of<ICommandRunner>(),
                cache,
                NullLogger<CSharpExecutionStrategy>.Instance);

            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.False(result.Success);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
