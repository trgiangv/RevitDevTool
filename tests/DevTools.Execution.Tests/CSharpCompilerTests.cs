using System.Reflection;
using System.Runtime.Loader;
using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using DevTools.Execution.Tests.AssemblyIsolation;
using DevTools.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class CSharpCompilerTests
{
    [Fact]
    public async Task CompileAsync_InlineScript_ReturnsSuccessWithCommand()
    {
        const string code = """
                              public sealed class ScriptCommand
                              {
                                  public static int M() => 1;
                              }
                              """;

        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));
        var bridge = ExecutionTestHelpers.CreateScriptBridge();

        var result = await compiler.CompileAsync(code, bridge, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.FormatDiagnostics());
        Assert.NotNull(result.Command);
        Assert.Equal(1, result.Command!.GetType().GetMethod("M")!.Invoke(result.Command, null));
        result.Cleanup?.Dispose();
    }

    [Fact]
    public async Task CompileAsync_FromFilePath_CompilesExistingCsx()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-compiler");
        var scriptPath = Path.Combine(directory, "sample_script.csx");
        await File.WriteAllTextAsync(
            scriptPath,
            "public sealed class ScriptCommand { public int Value => 42; }",
            TestContext.Current.CancellationToken);

        try
        {
            var compiler = new CSharpCompiler(
                NullLogger<CSharpCompiler>.Instance,
                new NugetManager(NullLogger<NugetManager>.Instance));

            var result = await compiler.CompileAsync(scriptPath, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.FormatDiagnostics());
            Assert.NotNull(result.Command);
            result.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CompileAsync_NoCommandType_ReturnsFailedResult()
    {
        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));
        var bridge = ExecutionTestHelpers.CreateScriptBridge("MissingCommand");

        var result = await compiler.CompileAsync(
            "public sealed class Other { public int Value => 1; }",
            bridge,
            ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("No host command type found", result.FormatDiagnostics());
    }

    [Fact]
    public async Task CompileAsync_FromFilePath_EmitsDebuggableSymbols()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-debug-symbols");
        var scriptPath = Path.Combine(directory, "sample_script.csx");
        await File.WriteAllTextAsync(
            scriptPath,
            """
            public sealed class ScriptCommand { public int Value => 42; }
            """,
            TestContext.Current.CancellationToken);

        try
        {
            var compiler = new CSharpCompiler(
                NullLogger<CSharpCompiler>.Instance,
                new NugetManager(NullLogger<NugetManager>.Instance));

            var result = await compiler.CompileAsync(scriptPath, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.FormatDiagnostics());
            var assembly = result.Command!.GetType().Assembly;
            var debuggable = assembly.GetCustomAttribute<System.Diagnostics.DebuggableAttribute>();
            Assert.NotNull(debuggable);
            Assert.True(debuggable!.IsJITOptimizerDisabled);
            result.Cleanup?.Dispose();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CompileAsync_AppliesHostVersionPreprocessorSymbols()
    {
        const string code = """
            public sealed class ScriptCommand
            {
            #if REVIT2025_OR_GREATER
                public int Value => 25;
            #else
                public int Value => 0;
            #endif
            #if REVIT2026_OR_GREATER
                public int Future => 1;
            #endif
            }
            """;

        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance),
            ExecutionTestHelpers.CreateHostAppInfo(HostApp.Revit, "2025"));

        var result = await compiler.CompileAsync(code, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success, result.FormatDiagnostics());
        var command = result.Command!;
        Assert.Equal(25, command.GetType().GetProperty("Value")!.GetValue(command));
        Assert.Null(command.GetType().GetProperty("Future"));
        result.Cleanup?.Dispose();
    }

    [Fact]
    public async Task CompileAsync_SkipsDuplicateSimpleNameLoadedInAppDomain()
    {
        var workload = CommandFixtureWorkload.Create(includeSibling: false);
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-dup-simple-name");
        var alcOne = new AssemblyLoadContext("csx-dup-1", isCollectible: true);
        var alcTwo = new AssemblyLoadContext("csx-dup-2", isCollectible: true);
        try
        {
            var copyOne = Path.Combine(directory, "one", "IsolationEntry.dll");
            var copyTwo = Path.Combine(directory, "two", "IsolationEntry.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(copyOne)!);
            Directory.CreateDirectory(Path.GetDirectoryName(copyTwo)!);
            File.Copy(workload.EntryPath, copyOne);
            File.Copy(workload.EntryPath, copyTwo);
            alcOne.LoadFromAssemblyPath(copyOne);
            alcTwo.LoadFromAssemblyPath(copyTwo);

            var compiler = new CSharpCompiler(
                NullLogger<CSharpCompiler>.Instance,
                new NugetManager(NullLogger<NugetManager>.Instance));

            var result = await compiler.CompileAsync(
                "public sealed class ScriptCommand { public int Value => 1; }",
                ExecutionTestHelpers.CreateScriptBridge(),
                ct: TestContext.Current.CancellationToken);

            Assert.True(result.Success, result.FormatDiagnostics());
            Assert.DoesNotContain("CS1704", result.FormatDiagnostics(), StringComparison.Ordinal);
            result.Cleanup?.Dispose();
        }
        finally
        {
            alcOne.Unload();
            alcTwo.Unload();
            TryDispose(workload);
            try { Directory.Delete(directory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void TryDispose(IDisposable disposable)
    {
        try
        {
            disposable.Dispose();
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
