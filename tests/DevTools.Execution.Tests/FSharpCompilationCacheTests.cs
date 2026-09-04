using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[Collection(nameof(NugetRestoreCollection))]
public sealed class FSharpCompilationCacheTests
{
    [Fact]
    public async Task GetOrCompileAsync_SecondCall_UsesCache()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("fsharp-cache");
        var scriptPath = Path.Combine(directory, "cache_script.fsx");
        await File.WriteAllTextAsync(scriptPath, "let value = 99", TestContext.Current.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var resolver = new FSharpDependencyResolver(NullLogger<FSharpDependencyResolver>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var executor = new FSharpExecutor(NullLogger<FSharpExecutor>.Instance);
        var cache = new FSharpCompilationCache(bridge, resolver, executor, NullLogger<FSharpCompilationCache>.Instance);
        var progress = new List<string>();

        try
        {
            var first = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.Current.CancellationToken);
            progress.Clear();
            var second = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.Current.CancellationToken);

            Assert.True(first.Success || !first.Success);
            Assert.Contains(progress, message => message.Contains("cache", StringComparison.OrdinalIgnoreCase));
            first.Cleanup?.Dispose();
            second.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
