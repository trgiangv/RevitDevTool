using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
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
}
