using System.Xml.Linq;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

public sealed class AssemblyBoundaryTests
{
    static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "Microsoft.Testing",
        "nunit",
        "xunit",
        "Autodesk",
        "System.Text.Json",
        "DevTools.Ipc",
        "System.Diagnostics.Process",
    ];

    [Fact]
    public void Abstractions_assembly_has_no_platform_or_framework_dependencies()
    {
        var names = typeof(TestingRunRequest).Assembly
            .GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .ToArray();

        var violations = names
            .Where(IsForbidden)
            .Select(name => $"referenced {name}")
            .ToList();

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void Abstractions_project_has_no_implementation_package_references()
    {
        var csproj = XDocument.Load(Path.Combine(FindRepositoryRoot(),
            "source", "DevTools.Testing.Abstractions", "DevTools.Testing.Abstractions.csproj"));
        var packages = csproj
            .Descendants()
            .Where(static element => element.Name.LocalName == "PackageReference")
            .Select(static element => element.Attribute("Include")?.Value)
            .Where(static identity => !string.IsNullOrWhiteSpace(identity))
            .ToArray();

        Assert.Empty(packages);
    }

    [Fact]
    public void Abstractions_source_does_not_mention_forbidden_implementation_types()
    {
        var directory = Path.Combine(FindRepositoryRoot(), "source", "DevTools.Testing.Abstractions");
        string[] forbidden =
        [
            "Microsoft.Testing",
            "NUnit.",
            "Xunit.",
            "Autodesk.",
            "System.Text.Json",
            "DevTools.Ipc",
            "System.Diagnostics.Process",
            "Process.Start",
        ];

        var sources = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path, directory))
            .ToArray();
        Assert.NotEmpty(sources);

        var violations = new List<string>();
        foreach (var path in sources)
        {
            var text = File.ReadAllText(path);
            foreach (var token in forbidden)
            {
                if (text.Contains(token, StringComparison.Ordinal))
                    violations.Add($"{Path.GetFileName(path)} contains {token}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    static bool IsForbidden(string assemblyName)
        => ForbiddenAssemblyPrefixes.Any(prefix =>
            assemblyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || assemblyName.Equals(prefix, StringComparison.OrdinalIgnoreCase));

    static bool IsBuildArtifact(string path, string projectDirectory)
    {
        var relativePath = Path.GetRelativePath(projectDirectory, path);
        var firstSegment = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
        return firstSegment is "bin" or "obj";
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
