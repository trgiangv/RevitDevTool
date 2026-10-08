using System.Runtime.CompilerServices;
using DevTools.Daemon.Mcp.Code;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class CodeModeCompilerTests
{
    [TestMethod]
    public void Compile_EmptyCode_Fails()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CodeModeCompiler.Compile("  "));
    }

    [TestMethod]
    public async Task Compile_ReturnOne_YieldsOne()
    {
        using var program = CodeModeCompiler.Compile("return 1;");

        var value = await program.RunAsync();

        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public async Task Compile_VerbatimQuoteDoubling_KeepsTheHostQuote()
    {
        using var program = CodeModeCompiler.Compile(
            """
            var code = @"message = ""OK"";";
            return code;
            """);

        var value = await program.RunAsync();

        Assert.AreEqual("message = \"OK\";", value);
    }

    [TestMethod]
    public void Compile_HashR_IsASyntaxError()
    {
        var error = Assert.ThrowsExactly<InvalidOperationException>(() =>
            CodeModeCompiler.Compile("#r \"System.Text.Json\"\nreturn 1;"));

        StringAssert.Contains(error.Message, "error");
    }

    [TestMethod]
    public void Compile_AssemblyUnloads()
    {
        var weak = CompileAndDrop();

        for (var attempt = 0; attempt < 10 && weak.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.IsFalse(weak.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CompileAndDrop()
    {
        var program = CodeModeCompiler.Compile("return 1;");
        var value = program.RunAsync().GetAwaiter().GetResult();
        Assert.AreEqual(1, value);
        var weak = new WeakReference(program.LoadContext);
        program.Dispose();
        return weak;
    }
}
