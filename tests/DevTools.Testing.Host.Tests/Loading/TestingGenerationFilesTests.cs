using DevTools.Testing.Host.Loading;

namespace DevTools.Testing.Host.Tests.Loading;

[TestClass]
public sealed class TestingGenerationFilesTests
{
    [TestMethod]
    [DataRow("sample.pdb")]
    [DataRow("native.dll")]
    [DataRow("readme.txt")]
    public void IsManagedIdentity_rejects_non_assemblies(string fileName)
    {
        using var workspace = new TemporaryDirectory();
        var path = Path.Combine(workspace.Path, fileName);
        File.WriteAllText(path, "not-a-pe");

        Assert.IsFalse(TestingGenerationFiles.IsManagedIdentity(path));
    }

    [TestMethod]
    public void Public_path_helpers_match_internal_generation_paths()
    {
        Assert.IsTrue(TestingGenerationFiles.IsVolatileGenerationOutput(@"TestResults\out.trx"));
        Assert.IsTrue(TestingGenerationFiles.IsVolatileGenerationOutput(@"Log\host.log"));
        Assert.IsFalse(TestingGenerationFiles.IsVolatileGenerationOutput(@"bin\sample.dll"));
    }

    [TestMethod]
    public void GetRelativePath_returns_a_path_under_the_root()
    {
        using var workspace = new TemporaryDirectory();
        var nested = Path.Combine(workspace.Path, "nested");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "sample.dll");
        File.WriteAllText(file, "x");

        var relative = TestingGenerationFiles.GetRelativePath(workspace.Path, file);
        Assert.IsFalse(Path.IsPathRooted(relative));
        Assert.Contains("sample.dll", relative, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void ContentEquals_returns_true_for_identical_files()
    {
        using var workspace = new TemporaryDirectory();
        var first = Path.Combine(workspace.Path, "first.bin");
        var second = Path.Combine(workspace.Path, "second.bin");
        File.WriteAllText(first, "same-content");
        File.WriteAllText(second, "same-content");

        Assert.IsTrue(TestingGenerationFiles.ContentEquals(first, second));
    }

    [TestMethod]
    public void ContentEquals_returns_false_for_different_files()
    {
        using var workspace = new TemporaryDirectory();
        var first = Path.Combine(workspace.Path, "first.bin");
        var second = Path.Combine(workspace.Path, "second.bin");
        File.WriteAllText(first, "left");
        File.WriteAllText(second, "right");

        Assert.IsFalse(TestingGenerationFiles.ContentEquals(first, second));
    }

    [TestMethod]
    public void MergeFile_replaces_existing_entry_only_when_content_differs()
    {
        using var workspace = new TemporaryDirectory();
        var original = Path.Combine(workspace.Path, "original.bin");
        var replacement = Path.Combine(workspace.Path, "replacement.bin");
        var unchanged = Path.Combine(workspace.Path, "unchanged.bin");
        File.WriteAllText(original, "version-one");
        File.WriteAllText(replacement, "version-two");
        File.WriteAllText(unchanged, "version-one");

        var files = new Dictionary<string, (string SourcePath, string RelativePath)>(StringComparer.OrdinalIgnoreCase)
        {
            ["asset.bin"] = (original, "asset.bin"),
        };

        TestingGenerationFiles.MergeFile(files, unchanged, "asset.bin");
        Assert.AreEqual(original, files["asset.bin"].SourcePath);

        TestingGenerationFiles.MergeFile(files, replacement, "asset.bin");
        Assert.AreEqual(replacement, files["asset.bin"].SourcePath);
    }

    [TestMethod]
    public void MergeRuntimeDependency_keeps_a_newer_consumer_assembly()
    {
        using var workspace = new TemporaryDirectory();
        var consumer = WriteAssembly(workspace.Path, "consumer", "Shared.Lib", new Version(4, 2, 4, 0));
        var runtime = WriteAssembly(workspace.Path, "runtime", "Shared.Lib", new Version(4, 2, 0, 1));
        var files = new Dictionary<string, (string SourcePath, string RelativePath)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shared.Lib.dll"] = (consumer, "Shared.Lib.dll"),
        };

        TestingGenerationFiles.MergeRuntimeDependency(files, runtime, "Shared.Lib.dll");

        Assert.AreEqual(consumer, files["Shared.Lib.dll"].SourcePath);
    }

    [TestMethod]
    public void MergeRuntimeDependency_replaces_an_older_or_equal_consumer_assembly()
    {
        using var workspace = new TemporaryDirectory();
        var older = WriteAssembly(workspace.Path, "older", "Shared.Lib", new Version(1, 0, 0, 0));
        var newer = WriteAssembly(workspace.Path, "newer", "Shared.Lib", new Version(2, 0, 0, 0));
        var equal = WriteAssembly(workspace.Path, "equal", "Shared.Lib", new Version(2, 0, 0, 0), marker: "different");
        var files = new Dictionary<string, (string SourcePath, string RelativePath)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Shared.Lib.dll"] = (older, "Shared.Lib.dll"),
        };

        TestingGenerationFiles.MergeRuntimeDependency(files, newer, "Shared.Lib.dll");
        Assert.AreEqual(newer, files["Shared.Lib.dll"].SourcePath);

        TestingGenerationFiles.MergeRuntimeDependency(files, equal, "Shared.Lib.dll");
        Assert.AreEqual(equal, files["Shared.Lib.dll"].SourcePath);
    }

    [TestMethod]
    public void MergeRuntimeDependency_adds_files_that_are_not_present()
    {
        using var workspace = new TemporaryDirectory();
        var runtime = WriteAssembly(workspace.Path, "runtime", "Shared.Lib", new Version(1, 0, 0, 0));
        var files = new Dictionary<string, (string SourcePath, string RelativePath)>(StringComparer.OrdinalIgnoreCase);

        TestingGenerationFiles.MergeRuntimeDependency(files, runtime, "Shared.Lib.dll");

        Assert.AreEqual(runtime, files["Shared.Lib.dll"].SourcePath);
    }

    static string WriteAssembly(string root, string folder, string name, Version version, string? marker = null)
    {
        var directory = System.IO.Path.Combine(root, folder);
        Directory.CreateDirectory(directory);
        var path = System.IO.Path.Combine(directory, name + ".dll");
        var builder = new System.Reflection.Emit.PersistedAssemblyBuilder(
            new System.Reflection.AssemblyName(name) { Version = version },
            typeof(object).Assembly);
        var module = builder.DefineDynamicModule(name);
        module.DefineType(marker ?? "Marker", System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class)
            .CreateType();
        builder.Save(path);
        return path;
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"generation-files-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
