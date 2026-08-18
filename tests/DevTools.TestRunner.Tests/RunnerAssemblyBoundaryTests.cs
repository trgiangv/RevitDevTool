using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DevTools.TestRunner.Tests;

public sealed class RunnerAssemblyBoundaryTests
{
    [Fact]
    public void Runner_core_is_framework_neutral()
    {
        var root = FindRepositoryRoot();
        var coreDirectory = Path.Combine(root, "source", "DevTools.TestRunner.Core");
        Assert.True(Directory.Exists(coreDirectory), "DevTools.TestRunner.Core must own framework-neutral runner infrastructure.");

        var coreFiles = Directory.EnumerateFiles(coreDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .ToList();

        Assert.NotEmpty(coreFiles);
        Assert.DoesNotContain(coreFiles, text => text.Contains("NUnit", StringComparison.Ordinal));
        Assert.DoesNotContain(coreFiles, text => text.Contains("nunit/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(coreFiles, text => text.Contains("nunit.framework", StringComparison.OrdinalIgnoreCase));
    }

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
        Assert.Contains("DevTools.TestRunner.Core.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Runner.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.Testing.Discovery", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.TestAdapter.csproj", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("MetadataTestDiscoverer.cs", csproj, StringComparison.Ordinal);

        var commands = File.ReadAllText(Path.Combine(
            root, "source", "DevTools.TestRunner", "Program.cs"));
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
    public void Runner_csharp_has_no_nunit_types_or_discover_command()
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "source", "DevTools.TestRunner");
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => part is "bin" or "obj"))
            .Select(path => (path, text: File.ReadAllText(path)))
            .ToList();

        Assert.NotEmpty(files);
        Assert.DoesNotContain(files, file => file.text.Contains("NUnit.", StringComparison.Ordinal)
            || file.text.Contains("using NUnit", StringComparison.Ordinal)
            || file.text.Contains("DevTools.NUnit", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.text.Contains("[Command(\"discover\")]", StringComparison.Ordinal));
        Assert.DoesNotContain(files, file => file.text.Contains("MetadataTestDiscoverer", StringComparison.Ordinal));
        Assert.Contains(files, file => file.text.Contains("[Command(\"run\")]", StringComparison.Ordinal));
    }

    [Fact]
    public void Installed_runner_keeps_TestRunner_exe_identity()
    {
        var root = FindRepositoryRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "source", "DevTools.TestRunner", "DevTools.TestRunner.csproj"));
        Assert.Contains("DevTools.TestRunner.exe", csproj, StringComparison.Ordinal);
        Assert.Contains("<AssemblyName>DevTools.TestRunner</AssemblyName>", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Runner.csproj", csproj, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(root, "source", "DevTools.TestRunner", "NUnit")));
        Assert.True(File.Exists(Path.Combine(root, "source", "DevTools.TestRunner", "RunnerCommands.cs")));
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
