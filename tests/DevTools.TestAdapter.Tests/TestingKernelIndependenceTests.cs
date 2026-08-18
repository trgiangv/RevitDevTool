namespace DevTools.TestAdapter.Tests;

public sealed class TestingKernelIndependenceTests
{
    [Fact]
    public void Generic_testing_projects_have_no_nunit_source_or_project_coupling()
    {
        var root = FindRepositoryRoot();
        var offenders = Directory.EnumerateDirectories(Path.Combine(root, "source"), "DevTools.Testing.*")
            .Where(project => Path.GetFileName(project).StartsWith("DevTools.Testing.", StringComparison.Ordinal))
            .SelectMany(project => Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories))
            .Where(path => Path.GetExtension(path) is ".cs" or ".csproj" or ".props" or ".targets")
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Where(path => File.ReadLines(path).Any(line => line.Contains("NUnit", StringComparison.OrdinalIgnoreCase)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Published_adapter_csharp_has_no_nunit_types()
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "source", "DevTools.TestAdapter");
        var offenders = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                    || part.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Where(path => File.ReadLines(path).Any(line =>
                line.Contains("NUnit.", StringComparison.Ordinal)
                || line.Contains("using NUnit", StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repository_has_no_standalone_testing_mtp_project()
    {
        var root = FindRepositoryRoot();
        Assert.False(Directory.Exists(Path.Combine(root, "source", "DevTools.Testing.Mtp")));
        Assert.False(Directory.Exists(Path.Combine(root, "tests", "DevTools.Testing.Mtp.Tests")));
        Assert.False(Directory.Exists(Path.Combine(root, "source", "DevTools.Testing")));
        Assert.True(Directory.Exists(Path.Combine(root, "source", "DevTools.TestAdapter")));
        Assert.False(Directory.Exists(Path.Combine(root, "source", "DevTools.Testing.Discovery")));
        Assert.False(Directory.Exists(Path.Combine(root, "source", "DevTools.TestRunner", "NUnit")));
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

        throw new InvalidOperationException("Could not locate RevitDevTool.slnx.");
    }
}
