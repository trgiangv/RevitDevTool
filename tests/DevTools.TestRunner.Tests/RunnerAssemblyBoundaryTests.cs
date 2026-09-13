using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace DevTools.TestRunner.Tests;

[TestClass]
public sealed class RunnerAssemblyBoundaryTests
{
    [TestMethod]
    public void Runner_is_framework_neutral()
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "source", "DevTools.TestRunner");
        var files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path)
                && (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)))
            .Select(File.ReadAllText)
            .ToList();

        Assert.IsNotEmpty(files);
        Assert.DoesNotContain(text => text.Contains("NUnit.", StringComparison.Ordinal)
            || text.Contains("using NUnit", StringComparison.Ordinal)
            || text.Contains("DevTools.NUnit", StringComparison.Ordinal)
            || text.Contains("nunit.framework", StringComparison.OrdinalIgnoreCase)
            || text.Contains("IRunnerCommandModule", StringComparison.Ordinal), files);
    }

    [TestMethod]
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
        Assert.DoesNotContain("DevTools.TestRunner.Core", csproj, StringComparison.Ordinal);
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
        Assert.IsFalse(string.IsNullOrEmpty(dll), "DevTools.TestRunner.dll was not built.");

        var references = ReadAssemblyReferences(dll!);
        Assert.DoesNotContain("DevTools.Logging", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Revit", references);
        Assert.DoesNotContain("DevTools.FileMetadata.Core", references);
        Assert.DoesNotContain("DevTools.TestRunner.Core", references);
        Assert.Contains("DevTools.Hosting", references);
    }

    [TestMethod]
    public void Runner_csharp_has_a_single_run_command_and_no_discover()
    {
        var root = FindRepositoryRoot();
        var directory = Path.Combine(root, "source", "DevTools.TestRunner");
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildArtifact(path))
            .Select(path => (path, text: File.ReadAllText(path)))
            .ToList();

        Assert.IsNotEmpty(files);
        Assert.DoesNotContain(file => file.text.Contains("NUnit.", StringComparison.Ordinal)
            || file.text.Contains("using NUnit", StringComparison.Ordinal)
            || file.text.Contains("DevTools.NUnit", StringComparison.Ordinal), files);
        Assert.DoesNotContain(file => file.text.Contains("[Command(\"discover\")]", StringComparison.Ordinal), files);
        Assert.DoesNotContain(file => file.text.Contains("[Command(\"machine-run\")]", StringComparison.Ordinal), files);
        Assert.DoesNotContain(file => file.text.Contains("MetadataTestDiscoverer", StringComparison.Ordinal), files);
        Assert.Contains(file => file.text.Contains("[Command(\"run\")]", StringComparison.Ordinal), files);
        Assert.DoesNotContain(
            file => file.text.Contains("[Command(\"run\")]", StringComparison.Ordinal)
                && file.text.Contains("[Argument] string assembly", StringComparison.Ordinal),
            files);
    }

    [TestMethod]
    public void Installed_runner_keeps_TestRunner_exe_identity()
    {
        var root = FindRepositoryRoot();
        var csproj = File.ReadAllText(Path.Combine(root, "source", "DevTools.TestRunner", "DevTools.TestRunner.csproj"));
        Assert.Contains("DevTools.TestRunner.exe", csproj, StringComparison.Ordinal);
        Assert.Contains("<AssemblyName>DevTools.TestRunner</AssemblyName>", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("DevTools.NUnit.Runner.csproj", csproj, StringComparison.Ordinal);
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "source", "DevTools.TestRunner", "NUnit")));
        Assert.IsTrue(File.Exists(Path.Combine(root, "source", "DevTools.TestRunner", "RunnerCommands.cs")));
        Assert.IsFalse(File.Exists(Path.Combine(root, "source", "DevTools.TestRunner.Core", "DevTools.TestRunner.Core.csproj")));
    }

    private static bool IsBuildArtifact(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part is "bin" or "obj");

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
