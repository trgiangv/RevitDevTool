using DevTools.Execution.Providers.CSharp;
using DevTools.Execution.Providers.FSharp;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class CSharpCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CSharpDirectiveParser_RewritesHostReference_WhenPatternMatches()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-host-rewrite");
        var dllPath = Path.Combine(directory, "RevitAPI_2024.dll");
        var rewrittenPath = Path.Combine(directory, "RevitAPI_2025.dll");
        File.WriteAllText(dllPath, "stub");
        File.WriteAllText(rewrittenPath, "stub");

        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(
            entryPath,
            $"""
            #r "{dllPath.Replace('\\', '/')}"
            Console.WriteLine("ok");
            """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(
                entryPath,
                path => path.Contains("RevitAPI_2024.dll", StringComparison.OrdinalIgnoreCase)
                    ? rewrittenPath
                    : path);

            Assert.AreEqual(1, graph.AssemblyReferences.Count);
            Assert.AreEqual(Path.GetFullPath(rewrittenPath), Path.GetFullPath(graph.AssemblyReferences[0]), ignoreCase: true);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CSharpDirectiveParser_IgnoresFrameworkReferenceSegments()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-ignored-ref");
        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(
            entryPath,
            """
            #r "C:/Program Files/dotnet/shared/Microsoft.NETCore.App/8.0.0/System.Runtime.dll"
            #r "nuget: Humanizer"
            """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);

            Assert.AreEqual(1, graph.Packages.Count);
            Assert.AreEqual("Humanizer", graph.Packages[0].PackageId);
            Assert.IsNull(graph.Packages[0].Version);
            Assert.IsEmpty(graph.AssemblyReferences);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CSharpDirectiveParser_SkipsMissingLoadTarget()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-missing-load");
        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(entryPath, "#load \"missing.csx\"");

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);
            Assert.AreEqual(1, graph.SourceFiles.Count);
            Assert.Contains("//", graph.SourceFiles[0].CleanSource, StringComparison.Ordinal);
            Assert.Contains("missing.csx", graph.SourceFiles[0].CleanSource, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CSharpDirectiveParser_AddsExistingAssemblyReference()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-asm-ref");
        var dllPath = Path.Combine(directory, "helper.dll");
        File.WriteAllBytes(dllPath, [0]);

        var entryPath = Path.Combine(directory, "entry.csx");
        File.WriteAllText(entryPath, $"""#r "{dllPath.Replace('\\', '/')}" """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);
            Assert.IsTrue(
                graph.AssemblyReferences.Any(
                    path => Path.GetFullPath(path).Equals(Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CSharpDirectiveParser_DeduplicatesVisitedFiles()
    {
        var directory = ExecutionTestHelpers.CreateTempDirectory("csharp-cycle");
        var entryPath = Path.Combine(directory, "entry.csx");
        var depPath = Path.Combine(directory, "dep.csx");
        File.WriteAllText(depPath, "#load \"entry.csx\"");
        File.WriteAllText(entryPath, "#load \"dep.csx\"");

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath);
            Assert.AreEqual(2, graph.SourceFiles.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void CSharpDirectiveParser_MissingEntryFile_ReturnsEmptyGraph()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"missing-entry-{Guid.NewGuid():N}.csx");
        var graph = CSharpDirectiveParser.ResolveGraph(missing);
        Assert.IsEmpty(graph.SourceFiles);
    }

    [TestMethod]
    public async Task CSharpCompiler_WithNugetReference_ResolvesAndCompiles()
    {
        const string code = """
                              #r "nuget: Newtonsoft.Json, 13.0.3"
                              public sealed class ScriptCommand
                              {
                                  public static int M() => 1;
                              }
                              """;

        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var result = await compiler.CompileAsync(code, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, result.FormatDiagnostics());
        Assert.IsNotNull(result.Command);
        result.Cleanup?.Dispose();
    }

    [TestMethod]
    public async Task CSharpCompiler_CompileSimpleCommand_Succeeds()
    {
        const string code = """
            public sealed class ScriptCommand
            {
                public static int Value => 11;
            }
            """;

        var compiler = new CSharpCompiler(
            NullLogger<CSharpCompiler>.Instance,
            new NugetManager(NullLogger<NugetManager>.Instance));

        var result = await compiler.CompileAsync(code, ExecutionTestHelpers.CreateScriptBridge(), ct: TestContext.CancellationToken);

        Assert.IsTrue(result.Success, result.FormatDiagnostics());
        result.Cleanup?.Dispose();
    }
}
