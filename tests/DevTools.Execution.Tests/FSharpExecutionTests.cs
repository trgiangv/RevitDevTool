using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevTools.Execution.Tests;

public sealed class FSharpExecutionTests
{
    [Fact]
    public async Task FSharpScriptGraph_BuildLoadGraph_FollowsLoadDirectives()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-graph");
        var childPath = Path.Combine(directory, "child.fsx");
        var entryPath = Path.Combine(directory, "entry_script.fsx");
        await File.WriteAllTextAsync(childPath, "type Child = class end", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(entryPath, $"#load @\"{childPath.Replace('\\', '/')}\"", TestContext.Current.CancellationToken);

        try
        {
            var graph = await FSharpScriptGraph.BuildLoadGraphAsync(entryPath, TestContext.Current.CancellationToken);

            Assert.Equal(2, graph.Nodes.Count);
            var normalizedKeys = graph.Nodes.Keys.Select(Path.GetFullPath).ToArray();
            Assert.Contains(Path.GetFullPath(entryPath), normalizedKeys, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(Path.GetFullPath(childPath), normalizedKeys, StringComparer.OrdinalIgnoreCase);
            Assert.NotEmpty(FSharpScriptGraph.ComputeGraphHash(graph));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FSharpExecutor_EvaluatesScriptAndDisposesSession()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-executor");
        var scriptPath = Path.Combine(directory, "sample_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let value = 99", TestContext.Current.CancellationToken);

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

    [Fact]
    public async Task FSharpCompilationCache_InvalidScript_ReturnsFailure()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-cache-fail");
        var scriptPath = Path.Combine(directory, "bad_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x =", TestContext.Current.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);

        try
        {
            var result = await cache.GetOrCompileAsync(scriptPath, ct: TestContext.Current.CancellationToken);
            Assert.False(result.Success);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FSharpExecutionStrategy_CompilationFailure_ReturnsFailedResult()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-strategy-fail");
        var scriptPath = Path.Combine(directory, "bad_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x =", TestContext.Current.CancellationToken);

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

            var result = await strategy.ExecuteAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(result.Success);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task FSharpDependencyResolver_NoRewrite_ReturnsOriginalScript()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-resolver");
        var scriptPath = Path.Combine(directory, "plain_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let x = 1", TestContext.Current.CancellationToken);
        var graph = await FSharpScriptGraph.BuildLoadGraphAsync(scriptPath, TestContext.Current.CancellationToken);
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));

        try
        {
            var resolution = await resolver.ResolveAsync(scriptPath, graph, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken);

            Assert.Equal(Path.GetFullPath(scriptPath), Path.GetFullPath(resolution.ScriptPath));
            Assert.Null(resolution.Cleanup);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
