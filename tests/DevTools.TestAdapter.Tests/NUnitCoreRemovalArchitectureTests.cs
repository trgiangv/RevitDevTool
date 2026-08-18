namespace DevTools.TestAdapter.Tests;

public sealed class NUnitCoreRemovalArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    private static readonly string[] InputRoots = ["source", "tests", "scripts", "build", "modules"];

    private static readonly HashSet<string> InputExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".csproj", ".props", ".targets", ".slnx", ".nuspec", ".ps1", ".psm1", ".json",
    };

    [Fact]
    public void Repository_has_no_active_DevTools_NUnit_Core_coupling()
    {
        var offenders = new List<string>();

        foreach (var path in EnumerateActiveInputs())
        {
            var relativePath = Normalize(Path.GetRelativePath(RepositoryRoot, path));
            if (ContainsCoreReference(relativePath) && !IsExplicitNegativeTest(path))
                offenders.Add("Core path: " + relativePath);

            foreach (var (line, index) in File.ReadLines(path).Select((line, index) => (line, index)))
            {
                if (ContainsCoreReference(Normalize(line)) && !IsExplicitNegativeTest(path))
                    offenders.Add($"active input: {relativePath}:{index + 1}: {line.Trim()}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "DevTools.NUnit.Core must be fully removed from active repository inputs:" + Environment.NewLine
            + string.Join(Environment.NewLine, offenders));
    }

    private static IEnumerable<string> EnumerateActiveInputs()
    {
        var rootSolution = Path.Combine(RepositoryRoot, "RevitDevTool.slnx");
        if (File.Exists(rootSolution))
            yield return rootSolution;

        foreach (var inputRoot in InputRoots)
        {
            var root = Path.Combine(RepositoryRoot, inputRoot);
            if (!Directory.Exists(root))
                continue;

            foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(IsActiveInput))
                yield return path;
        }
    }

    private static bool IsActiveInput(string path)
    {
        if (!InputExtensions.Contains(Path.GetExtension(path)))
            return false;

        // Build output, git metadata, and generated task artifacts are not repository inputs.
        // A Core file in any remaining source/test/build/package path is an active coupling.
        return !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, ".git", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, ".superpowers", StringComparison.OrdinalIgnoreCase)
                || string.Equals(part, "TestResults", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsCoreReference(string value) =>
        value.Contains("DevTools.NUnit.Core", StringComparison.OrdinalIgnoreCase)
        || value.Contains("DevTools/NUnit.Core", StringComparison.OrdinalIgnoreCase)
        || value.Contains("DevTools\\NUnit.Core", StringComparison.OrdinalIgnoreCase);

    private static bool IsExplicitNegativeTest(string path)
    {
        var relativePath = Normalize(Path.GetRelativePath(RepositoryRoot, path));
        return relativePath is
            "tests/DevTools.TestAdapter.Tests/NUnitCoreRemovalArchitectureTests.cs" or
            "tests/DevTools.TestAdapter.Tests/PackageConsumerTests.cs" or
            "tests/DevTools.NUnit.Host.Tests/HostPackagingOwnershipTests.cs";
    }

    private static string Normalize(string value) => value.Replace('\\', '/');
}
