using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerAssemblyBoundaryTests
{
    [Fact]
    public void Runner_does_not_reference_logging()
    {
        var root = FindRepositoryRoot();
        var csproj = File.ReadAllText(Path.Combine(
            root,
            "source",
            "DevTools.TestRunner",
            "DevTools.TestRunner.csproj"));
        Assert.DoesNotContain("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("FileMetadata", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft-WindowsAPICodePack-Shell", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Utilities.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Hosting.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions.DependencyInjection", csproj, StringComparison.Ordinal);

        var commands = File.ReadAllText(Path.Combine(
            root, "source", "DevTools.TestRunner", "Commands", "TestRunnerCommands.cs"));
        Assert.DoesNotContain("new HostLaunchService()", commands, StringComparison.Ordinal);

        var dll = Directory.GetFiles(
                Path.Combine(root, "source", "DevTools.TestRunner", "bin"),
                "DevTools.TestRunner.dll",
                SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(dll), "DevTools.TestRunner.dll was not built.");

        var references = ReadAssemblyReferences(dll!);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Revit", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Core", references);
        Assert.Contains("DevTools.Hosting", references);
    }

    [Fact]
    public void Installed_runner_is_TestRunner_exe_not_legacy_nunit_runner()
    {
        var root = FindRepositoryRoot();
        var legacyName = "DevTools.NUnit" + ".Runner";
        var operationalRoots = new[]
        {
            Path.Combine(root, "source"),
            Path.Combine(root, "tests"),
            Path.Combine(root, "build"),
            Path.Combine(root, "samples"),
            Path.Combine(root, "docs", "product"),
            Path.Combine(root, "docs", "agents"),
        };

        var offenders = operationalRoots
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".targets", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains(legacyName, StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Legacy runner name remains in deployed/project paths:" + Environment.NewLine + string.Join(Environment.NewLine, offenders));

        var csproj = File.ReadAllText(Path.Combine(root, "source", "DevTools.TestRunner", "DevTools.TestRunner.csproj"));
        Assert.Contains("DevTools.TestRunner.exe", csproj, StringComparison.Ordinal);
        Assert.Contains("<AssemblyName>DevTools.TestRunner</AssemblyName>", csproj, StringComparison.Ordinal);
    }

    private static HashSet<string> ReadAssemblyReferences(string dllPath)
    {
        using var stream = File.OpenRead(dllPath);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            names.Add(reader.GetString(reference.Name));
        }

        return names;
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

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
