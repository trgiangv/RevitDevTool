namespace DevTools.NUnit.Mtp.Tests;

public sealed class MtpOnlyArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Repository_has_no_NUnit_VSTest_product_surface()
    {
        var forbiddenPaths = new[]
        {
            "source/DevTools.NUnit.TestAdapter",
            "tests/DevTools.NUnit.TestAdapter.Tests",
            "samples/DevTools.NUnit.VSTest.SampleTests",
            "samples/DevTools.NUnit.VSTest.Civil3D.SampleTests",
        };

        var offenders = forbiddenPaths
            .Where(path =>
            {
                var directory = Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar));
                return Directory.Exists(directory)
                    && Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any(IsActiveInput);
            })
            .ToList();

        offenders.AddRange(FindTextReferences(
            "DevTools.NUnit.TestAdapter",
            "DevTools.NUnit.VSTest.SampleTests",
            "DevTools.NUnit.VSTest.Civil3D.SampleTests"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Repository_test_projects_use_MTP_without_VSTest_packages()
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
        Assert.Empty(offenders);
    }

    [Fact]
    public void NUnit_host_stack_has_no_legacy_protocol_or_transport()
    {
        var transport = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Transport");
        var offenders = new List<string>();
        if (Directory.Exists(transport)
            && Directory.EnumerateFiles(transport, "*", SearchOption.AllDirectories).Any(IsActiveInput))
            offenders.Add("source/DevTools.NUnit.Transport");

        offenders.AddRange(FindTextReferences(
            "DevTools.NUnit.Transport",
            "NUnitProtocol",
            "NUnitRequestHandler",
            "NUnitPipeClient",
            "INUnitRuntimeSession"));

        Assert.Empty(offenders);
    }

    [Fact]
    public void Testing_stack_has_no_netstandard_compatibility_target()
    {
        var projects = new[]
        {
            "source/DevTools.Ipc/DevTools.Ipc.csproj",
            "source/DevTools.Testing.Abstractions/DevTools.Testing.Abstractions.csproj",
            "source/DevTools.Testing.Transport/DevTools.Testing.Transport.csproj",
            "source/DevTools.NUnit.Discovery/DevTools.NUnit.Discovery.csproj",
            "source/DevTools.NUnit.Mtp/DevTools.NUnit.Mtp.csproj",
        };

        var offenders = projects
            .Where(path => File.ReadAllText(Path.Combine(RepositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
                .Contains("netstandard", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(offenders);
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
        string.Equals(Path.GetFileName(path), nameof(MtpOnlyArchitectureTests) + ".cs", StringComparison.Ordinal);

    private static bool IsRicaunComparisonSample(string offender)
    {
        if (offender.StartsWith("samples/ricaun.NUnit.SampleTests/", StringComparison.Ordinal))
            return true;

        return offender.StartsWith("Directory.Packages.props:", StringComparison.Ordinal)
            && (offender.Contains("ricaun.RevitTest.TestAdapter", StringComparison.Ordinal)
                || offender.Contains("Microsoft.NET.Test.Sdk", StringComparison.Ordinal));
    }
}
