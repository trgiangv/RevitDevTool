using DevTools.Execution.Providers.CSharp;

namespace DevTools.Execution.Tests;

public sealed class CSharpDirectiveParserTests
{
    [Fact]
    public void ResolveGraph_EntryWithNugetAndLoad_IncludesDependencies()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"csharp-directive-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        var dependencyPath = Path.Combine(tempDirectory, "dep.csx");
        var entryPath = Path.Combine(tempDirectory, "entry.csx");
        File.WriteAllText(dependencyPath, "// dependency");
        File.WriteAllText(
            entryPath,
            """
            #r "nuget: Newtonsoft.Json, 13.0.3"
            #load "dep.csx"
            Console.WriteLine("entry");
            """);

        try
        {
            var graph = CSharpDirectiveParser.ResolveGraph(entryPath, hostPattern: null, hostReplacement: null);

            Assert.Equal(2, graph.SourceFiles.Count);
            Assert.Equal(dependencyPath, graph.SourceFiles[0].Path, ignoreCase: true);
            Assert.Equal(entryPath, graph.SourceFiles[1].Path, ignoreCase: true);
            Assert.Contains(
                graph.Packages,
                package => package.PackageId == "Newtonsoft.Json" && package.Version == "13.0.3");
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

}
