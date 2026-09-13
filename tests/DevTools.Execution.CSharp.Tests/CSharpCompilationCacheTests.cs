using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class CSharpCompilationCacheTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task GetOrCompileAsync_SecondCall_UsesCache()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-cache");
        var scriptPath = Path.Combine(directory, "cache_script.csx");
        await File.WriteAllTextAsync(
            scriptPath,
            "public sealed class ScriptCommand { public static int M() => 7; }",
            TestContext.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var compiler = new CSharpCompiler(NullLogger<CSharpCompiler>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var cache = new CSharpCompilationCache(bridge, compiler, NullLogger<CSharpCompilationCache>.Instance);
        var progress = new List<string>();

        try
        {
            var first = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.CancellationToken);
            var second = await cache.GetOrCompileAsync(scriptPath, new Progress<string>(progress.Add), TestContext.CancellationToken);

            Assert.IsTrue(first.Success, first.FormatDiagnostics());
            Assert.IsTrue(second.Success, second.FormatDiagnostics());
            var compileCount = progress.Count(message => message.Contains("Compiling", StringComparison.Ordinal));
            Assert.AreEqual(1, compileCount);
            first.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task GetOrCompileAsync_FileChange_Recompiles()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-cache-miss");
        var scriptPath = Path.Combine(directory, "mutating_script.csx");
        await File.WriteAllTextAsync(
            scriptPath,
            "public sealed class ScriptCommand { public static int M() => 1; }",
            TestContext.CancellationToken);

        var bridge = ExecutionTestHelpers.CreateScriptBridge();
        var compiler = new CSharpCompiler(NullLogger<CSharpCompiler>.Instance, new NugetManager(NullLogger<NugetManager>.Instance));
        var cache = new CSharpCompilationCache(bridge, compiler, NullLogger<CSharpCompilationCache>.Instance);

        try
        {
            var first = await cache.GetOrCompileAsync(scriptPath, ct: TestContext.CancellationToken);
            await File.WriteAllTextAsync(
                scriptPath,
                "public sealed class ScriptCommand { public static int M() => 2; }",
                TestContext.CancellationToken);
            var second = await cache.GetOrCompileAsync(scriptPath, ct: TestContext.CancellationToken);

            Assert.IsTrue(first.Success);
            Assert.IsTrue(second.Success);
            Assert.AreEqual(1, first.Command!.GetType().GetMethod("M")!.Invoke(first.Command, null));
            Assert.AreEqual(2, second.Command!.GetType().GetMethod("M")!.Invoke(second.Command, null));
            first.Cleanup?.Dispose();
            second.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
