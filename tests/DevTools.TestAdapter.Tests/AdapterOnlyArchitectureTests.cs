namespace DevTools.TestAdapter.Tests;

[TestClass]
public sealed class AdapterOnlyArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [TestMethod]
    public void Test_projects_have_no_VSTest_packages()
    {
        var forbiddenPackages = new[]
        {
            "Microsoft.NET.Test.Sdk",
            "Microsoft.TestPlatform.ObjectModel",
            "xunit.runner.visualstudio",
            "NUnit3TestAdapter",
            "ricaun.RevitTest.TestAdapter",
        };

        var offenders = FindTextReferences(forbiddenPackages)
            .Where(line => !IsRicaunComparisonSample(line))
            .ToList();
        Assert.IsEmpty(offenders);
    }

    [TestMethod]
    public void Testing_stack_has_no_netstandard_target()
    {
        var projects = new[]
        {
            "source/DevTools.Ipc/DevTools.Ipc.csproj",
            "source/DevTools.Testing.Abstractions/DevTools.Testing.Abstractions.csproj",
            "source/DevTools.Testing.Transport/DevTools.Testing.Transport.csproj",
            "source/DevTools.TestAdapter/DevTools.TestAdapter.csproj",
        };

        var offenders = projects
            .Where(path => File.ReadAllText(Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
                .Contains("netstandard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.IsEmpty(offenders);
    }

    private static List<string> FindTextReferences(params string[] values)
    {
        var roots = new[] { "Directory.Packages.props", "RevitDevTool.slnx", "source", "tests", "samples", "build", "scripts" };
        var offenders = new List<string>();

        foreach (var relativeRoot in roots)
        {
            var root = Path.Combine(RepositoryRoot, relativeRoot);
            var paths = File.Exists(root)
                ? new[] { root }
                : Directory.Exists(root)
                    ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsActiveInput)
                    : [];

            foreach (var path in paths)
            {
                if (IsThisTest(path))
                    continue;

                var relativePath = Path.GetRelativePath(RepositoryRoot, path).Replace('\\', '/');
                foreach (var (line, index) in File.ReadLines(path).Select((line, index) => (line, index + 1)))
                {
                    if (values.Any(value => line.Contains(value, StringComparison.OrdinalIgnoreCase)))
                        offenders.Add($"{relativePath}:{index}: {line.Trim()}");
                }
            }
        }

        return offenders;
    }

    private static bool IsActiveInput(string path)
    {
        var extension = Path.GetExtension(path);
        if (extension is not (".cs" or ".csproj" or ".props" or ".targets" or ".slnx" or ".md" or ".ps1"))
            return false;

        return !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj" or ".git" or ".superpowers" or "TestResults");
    }

    private static bool IsThisTest(string path) =>
        string.Equals(Path.GetFileName(path), nameof(AdapterOnlyArchitectureTests) + ".cs", StringComparison.Ordinal);

    private static bool IsRicaunComparisonSample(string offender)
    {
        if (offender.StartsWith("samples/ricaun.NUnit.SampleTests/", StringComparison.Ordinal))
            return true;

        return offender.StartsWith("Directory.Packages.props:", StringComparison.Ordinal)
            && (offender.Contains("ricaun.RevitTest.TestAdapter", StringComparison.Ordinal)
                || offender.Contains("Microsoft.NET.Test.Sdk", StringComparison.Ordinal));
    }
}
