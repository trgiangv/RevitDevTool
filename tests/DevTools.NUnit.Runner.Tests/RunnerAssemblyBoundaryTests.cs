using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DevTools.NUnit.Runner.Tests;

public sealed class RunnerAssemblyBoundaryTests
{
    [Fact]
    public void Runner_does_not_reference_logging()
    {
        var root = FindRepositoryRoot();
        var csproj = File.ReadAllText(Path.Combine(
            root,
            "source",
            "DevTools.NUnit.Runner",
            "DevTools.NUnit.Runner.csproj"));
        Assert.DoesNotContain("DevTools.Logging.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("FileMetadata", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("Microsoft-WindowsAPICodePack-Shell", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Utilities.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("DevTools.Hosting.csproj", csproj, StringComparison.Ordinal);
        Assert.Contains("Microsoft.Extensions.DependencyInjection", csproj, StringComparison.Ordinal);

        var commands = File.ReadAllText(Path.Combine(
            root, "source", "DevTools.NUnit.Runner", "Commands", "NUnitRunnerCommands.cs"));
        Assert.DoesNotContain("new HostLaunchService()", commands, StringComparison.Ordinal);

        var dll = Directory.GetFiles(
                Path.Combine(root, "source", "DevTools.NUnit.Runner", "bin"),
                "DevTools.NUnit.Runner.dll",
                SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(dll), "DevTools.NUnit.Runner.dll was not built.");

        var references = ReadAssemblyReferences(dll!);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Revit", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Core", references);
        Assert.Contains("DevTools.Hosting", references);
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
