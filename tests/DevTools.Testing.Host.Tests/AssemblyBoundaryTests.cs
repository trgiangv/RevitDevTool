using System.Xml.Linq;
using DevTools.Testing.Host;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class AssemblyBoundaryTests
{
    [TestMethod]
    public void Host_project_does_not_reference_nunit_xunit_or_autodesk()
    {
        var csproj = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.Testing.Host",
            "DevTools.Testing.Host.csproj"));
        var packageReferences = csproj
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToList();
        var projectReferences = csproj
            .Descendants()
            .Where(static element => element.Name.LocalName == "ProjectReference")
            .ToList();
        var projectIncludes = projectReferences
            .Select(static element => element.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        Assert.IsFalse(packageReferences.Any(value => value.Contains("NUnit", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(packageReferences.Any(value => value.Contains("TUnit", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(packageReferences.Any(value => value.Contains("xunit", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(packageReferences.Any(value => value.Contains("Autodesk", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(projectIncludes.Any(value => value.Contains("DevTools.Testing.Abstractions", StringComparison.Ordinal)));
        Assert.IsTrue(projectIncludes.Any(value => value.Contains("DevTools.Testing.Transport", StringComparison.Ordinal)));
        Assert.IsTrue(projectIncludes.Any(value => value.Contains("DevTools.Hosting", StringComparison.Ordinal)));
        Assert.IsTrue(projectIncludes.Any(value => value.Contains("DevTools.Execution.Abstractions", StringComparison.Ordinal)));
        Assert.IsTrue(projectIncludes.Any(value => value.Contains("DevTools.NUnit.Runtime", StringComparison.Ordinal)));
        Assert.IsTrue(projectIncludes.Any(value => value.Contains("DevTools.TUnit.Runtime", StringComparison.Ordinal)));
        foreach (var element in projectReferences.Where(static element =>
                 {
                     var include = element.Attribute("Include")?.Value ?? string.Empty;
                     return include.Contains("DevTools.NUnit.Runtime", StringComparison.Ordinal)
                         || include.Contains("DevTools.TUnit.Runtime", StringComparison.Ordinal);
                 }))
        {
            Assert.AreEqual(
                "false",
                element.Attribute("ReferenceOutputAssembly")?.Value,
                ignoreCase: true);
        }
    }

    [TestMethod]
    public void Host_source_has_no_discovery_or_host_locate_api()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Host");
        string[] forbidden =
        [
            "HostLocator",
            "EnsurePipeAsync",
            "Process.Start",
        ];

        var violations = new List<string>();
        foreach (var path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{Path.GetFileName(path)} contains {token}");
            }
        }

        Assert.IsTrue(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
