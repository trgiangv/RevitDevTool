namespace DevTools.Hosting.Tests;

public sealed class AssemblyIsolationAddinBoundaryTests
{
    [Fact]
    public void Revit_addin_uses_kernel_metadata_discovery_and_owns_one_resolver_lifecycle()
    {
        AssertAddinBoundary("RevitDevTool", "RevitCommandDiscovery.cs");
    }

    [Fact]
    public void Acad_addin_uses_kernel_metadata_discovery_and_owns_one_resolver_lifecycle()
    {
        AssertAddinBoundary("ACadDevTool", "AcadCommandDiscovery.cs");
    }

    static void AssertAddinBoundary(string projectName, string discoveryFileName)
    {
        var root = RepositoryRoot.Find();
        var projectDirectory = Path.Combine(root, "source", projectName);
        var discoveryPath = Directory.GetFiles(projectDirectory, discoveryFileName, SearchOption.AllDirectories).Single();
        var discoverySource = File.ReadAllText(discoveryPath);
        var applicationSource = File.ReadAllText(Path.Combine(projectDirectory, "Application.cs"));
        var projectFile = File.ReadAllText(Directory.GetFiles(projectDirectory, "*.csproj").Single());

        Assert.Contains("MetadataAssemblySession.Create", discoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataLoadContext", discoverySource, StringComparison.Ordinal);
        Assert.DoesNotContain("PathAssemblyResolver", discoverySource, StringComparison.Ordinal);
        Assert.Contains("PermanentDirectoryAssemblyResolver", applicationSource, StringComparison.Ordinal);
        Assert.Contains("PermanentAssemblyLoader(new", applicationSource, StringComparison.Ordinal);
        Assert.Contains("_assemblyResolver ??=", applicationSource, StringComparison.Ordinal);
        Assert.Contains("_assemblyResolver.Register()", applicationSource, StringComparison.Ordinal);
        Assert.Contains("_assemblyResolver?.Dispose()", applicationSource, StringComparison.Ordinal);
        Assert.Contains("_assemblyResolver = null", applicationSource, StringComparison.Ordinal);
        Assert.Contains("DevTools.AssemblyIsolation", projectFile, StringComparison.Ordinal);
    }
}
