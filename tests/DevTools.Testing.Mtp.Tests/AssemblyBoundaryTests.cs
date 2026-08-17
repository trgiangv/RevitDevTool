using System.Xml.Linq;
using DevTools.Testing.Mtp;

namespace DevTools.Testing.Mtp.Tests;

public sealed class AssemblyBoundaryTests
{
    [Fact]
    public void Mtp_project_does_not_reference_nunit_xunit_or_autodesk()
    {
        var csproj = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "source",
            "DevTools.Testing.Mtp",
            "DevTools.Testing.Mtp.csproj"));
        var references = csproj
            .Descendants()
            .Where(static element =>
                element.Name.LocalName is "ProjectReference" or "PackageReference")
            .Select(static element =>
                element.Attribute("Include")?.Value ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(references, value => value.Contains("NUnit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value.Contains("xunit", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value.Contains("Autodesk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(references, value => value.Contains("Hosting", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(references, value => value.Contains("Microsoft.Testing.Platform", StringComparison.Ordinal));
        Assert.Contains(references, value => value.Contains("DevTools.Testing.Abstractions", StringComparison.Ordinal));
        Assert.Contains(references, value => value.Contains("DevTools.Testing.Transport", StringComparison.Ordinal));
    }

    [Fact]
    public void Mtp_source_does_not_start_processes_or_register_a_universal_framework()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Mtp");
        string[] forbidden =
        [
            "Process.Start",
            "ITestFramework",
            "RegisterTestFramework",
            "HostLocator",
            "NUnit.",
            "Xunit.",
            "Autodesk.",
        ];

        var violations = new List<string>();
        foreach (var path in Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            if (IsGenerated(directory, path))
                continue;

            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{Path.GetFileName(path)} contains {token}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    static bool IsGenerated(string projectDirectory, string path)
    {
        var relative = Path.GetRelativePath(projectDirectory, path);
        return relative.StartsWith("obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("obj" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("bin" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
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
