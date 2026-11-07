using DevTools.Testing.Host.Loading;
namespace DevTools.NUnit.Host.Tests;

[TestClass]
public sealed class HostAssemblyBoundaryTests
{
    [TestMethod]
    public void Host_has_no_cad_or_logging_references()
    {
        var csproj = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.Testing.Host",
            "DevTools.Testing.Host.csproj"));
        Assert.DoesNotContain("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("ZLogger.Scintilla", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scintilla5", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DevTools.AssemblyIsolation.csproj", csproj, StringComparison.Ordinal);

        var loadingDirectory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Host", "NUnit");
        string[] forbiddenHostApiNames =
        [
            "RevitDBAPI",
            "RevitAPI.dll",
            "RevitAPIUI",
            "accoremgd",
            "Acdbmgd",
            "acmgd.dll",
        ];
        var hostApiNameHits = Directory
            .GetFiles(loadingDirectory, "*.cs", SearchOption.AllDirectories)
            .SelectMany(path => forbiddenHostApiNames
                .Where(name => File.ReadAllText(path).Contains(name, StringComparison.OrdinalIgnoreCase))
                .Select(name => $"{Path.GetRelativePath(FindRepositoryRoot(), path)} contains {name}"))
            .ToList();
        Assert.IsTrue(hostApiNameHits.Count == 0, string.Join(Environment.NewLine, hostApiNameHits));

        Assert.AreEqual("DevTools.Testing.Host", typeof(OutputGeneration).Assembly.GetName().Name);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test output.");
    }
}
