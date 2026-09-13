using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class FSharpExecutionTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task FSharpScriptGraph_BuildLoadGraph_FollowsLoadDirectives()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-graph");
        var childPath = Path.Combine(directory, "child.fsx");
        var entryPath = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(childPath, "type Child = class end", TestContext.CancellationToken);
        await File.WriteAllTextAsync(entryPath, $"#load @\"{childPath.Replace('\\', '/')}\"", TestContext.CancellationToken);

        try
        {
            var graph = await FSharpScriptGraph.BuildLoadGraphAsync(entryPath, TestContext.CancellationToken);

            Assert.AreEqual(2, graph.Nodes.Count);
            var normalizedKeys = graph.Nodes.Keys.Select(Path.GetFullPath).ToArray();
            Assert.Contains(
                key => string.Equals(key, Path.GetFullPath(entryPath), StringComparison.OrdinalIgnoreCase),
                normalizedKeys);
            Assert.Contains(
                key => string.Equals(key, Path.GetFullPath(childPath), StringComparison.OrdinalIgnoreCase),
                normalizedKeys);
            Assert.IsNotEmpty(FSharpScriptGraph.ComputeGraphHash(graph));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FSharpExecutor_EvaluatesScriptAndDisposesSession()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-executor");
        var scriptPath = Path.Combine(directory, "sample_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let value = 99", TestContext.CancellationToken);

        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var bridge = ExecutionTestHelpers.CreateScriptBridge();

        try
        {
            var output = executor.CreateSessionAndEvaluate(scriptPath, [], bridge);
            (output.Session as IDisposable)?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FSharpExecutor_AppliesHostVersionPreprocessorSymbols()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-defines");
        var scriptPath = Path.Combine(directory, "defines_script.fsx");
        await File.WriteAllTextAsync(
            scriptPath,
            """
            #if REVIT2026_OR_GREATER
            let __should_not_compile_on_2025 : int = "nope"
            #endif

            type ScriptCommand() =
                member _.Value =
            #if REVIT2025_OR_GREATER
                    25
            #else
                    0
            #endif
            """,
            TestContext.CancellationToken);

        var executor = new FSharpExecutor(
            NullLogger<FSharpExecutor>.Instance,
            ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "2025"));
        var bridge = ExecutionTestHelpers.CreateScriptBridge();

        try
        {
            var output = executor.CreateSessionAndEvaluate(scriptPath, [], bridge);
            Assert.IsNotNull(output.Command);
            Assert.AreEqual(25, output.Command!.GetType().GetProperty("Value")!.GetValue(output.Command));
            (output.Session as IDisposable)?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FSharpCompilationCache_InvalidScript_ReturnsFailure()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-cache-fail");
        var scriptPath = Path.Combine(directory, "bad_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x =", TestContext.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);

        try
        {
            var result = await cache.GetOrCompileAsync(scriptPath, ct: TestContext.CancellationToken);
            Assert.IsFalse(result.Success);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task FSharpExecutionStrategy_CompilationFailure_ReturnsFailedResult()
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

    [TestMethod]
    public async Task FSharpDependencyResolver_NoRewrite_ReturnsOriginalScript()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-resolver");
        var scriptPath = Path.Combine(directory, "plain_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x = 1", TestContext.CancellationToken);
        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.CancellationToken);
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(scriptPath, graph, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.CancellationToken);

            Assert.AreEqual(Path.GetFullPath(scriptPath), Path.GetFullPath(resolution.ScriptPath));
            Assert.IsNull(resolution.Cleanup);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
