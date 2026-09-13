using System.Reflection;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class ManagedAssemblySourceTests
{
    [TestMethod]
    public void Manifest_lookup_selects_by_full_identity()
    {
        using var directory = new TemporaryDirectory();
        var path = CopyAssembly(directory.Path, typeof(ManagedAssemblySourceTests).Assembly);
        var identity = typeof(ManagedAssemblySourceTests).Assembly.GetName();
        var source = new ManifestAssemblySource(
        [
            (identity, new AssemblyCandidate(path, directory.Path)),
        ]);

        var differentVersion = new AssemblyName(identity.FullName!) { Version = new Version(99, 0, 0, 0) };

        Assert.IsNull(source.Resolve(differentVersion));
        Assert.AreEqual(Path.GetFullPath(path), source.Resolve(identity)!.Path);
    }

    [TestMethod]
    public void Manifest_resolves_distinct_versions_of_the_same_simple_name()
    {
        using var directory = new TemporaryDirectory();
        var olderPath = Path.Combine(directory.Path, "System.Text.Json.v9.dll");
        var newerPath = Path.Combine(directory.Path, "System.Text.Json.dll");
        File.WriteAllText(olderPath, "placeholder");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (olderIdentity, new AssemblyCandidate(olderPath, directory.Path)),
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.AreEqual(Path.GetFullPath(olderPath), source.Resolve(olderIdentity)!.Path);
        Assert.AreEqual(Path.GetFullPath(newerPath), source.Resolve(newerIdentity)!.Path);
    }

    [TestMethod]
    public void Duplicate_compatible_manifest_candidates_use_the_first_declared_candidate()
    {
        using var directory = new TemporaryDirectory();
        var firstPath = CopyAssembly(directory.Path, typeof(ManagedAssemblySourceTests).Assembly, "first.dll");
        var secondPath = CopyAssembly(directory.Path, typeof(ManagedAssemblySourceTests).Assembly, "second.dll");
        var identity = typeof(ManagedAssemblySourceTests).Assembly.GetName();
        var source = new ManifestAssemblySource(
        [
            (identity, new AssemblyCandidate(firstPath, directory.Path)),
            (identity, new AssemblyCandidate(secondPath, directory.Path)),
        ]);

        var candidate = source.Resolve(identity);

        Assert.IsNotNull(candidate);
        Assert.AreEqual(Path.GetFullPath(firstPath), candidate.Path);
    }

    [TestMethod]
    public void System_text_json_candidate_is_not_implicitly_shared()
    {
        using var directory = new TemporaryDirectory();
        var identity = new AssemblyName("System.Text.Json, Version=99.0.0.0");
        var candidate = new AssemblyCandidate(Path.Combine(directory.Path, "System.Text.Json.dll"), directory.Path);
        var source = new ManifestAssemblySource([(identity, candidate)]);

        Assert.AreSame(candidate, source.Resolve(identity));
    }

    [TestMethod]
    public void Microsoft_extensions_candidate_is_not_implicitly_shared()
    {
        using var directory = new TemporaryDirectory();
        var identity = new AssemblyName("Microsoft.Extensions.Configuration, Version=99.0.0.0");
        var candidate = new AssemblyCandidate(Path.Combine(directory.Path, "Microsoft.Extensions.Configuration.dll"), directory.Path);
        var source = new ManifestAssemblySource([(identity, candidate)]);

        Assert.AreSame(candidate, source.Resolve(identity));
    }

    [TestMethod]
    public void Directory_source_is_lazy_and_does_not_preload_siblings()
    {
        using var directory = new TemporaryDirectory();
        var assemblyPath = CopyAssembly(directory.Path, typeof(ManagedAssemblySourceTests).Assembly);
        CopyAssembly(directory.Path, typeof(AssemblyIdentityMatcherTests).Assembly, "sibling.dll");
        var before = AppDomain.CurrentDomain.GetAssemblies().Length;

        _ = new DirectoryAssemblySource(directory.Path);

        Assert.AreEqual(before, AppDomain.CurrentDomain.GetAssemblies().Length);
        Assert.IsTrue(File.Exists(assemblyPath));
    }

    [TestMethod]
    public void Directory_source_rejects_traversal_outside_its_allowed_root()
    {
        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        var identity = typeof(ManagedAssemblySourceTests).Assembly.GetName();
        CopyAssembly(outside.Path, typeof(ManagedAssemblySourceTests).Assembly, $"{identity.Name}.dll");
        var source = new DirectoryAssemblySource(root.Path);

        var traversal = new AssemblyName(identity.FullName!) { Name = Path.Combine("..", Path.GetFileName(outside.Path), identity.Name!) };

        Assert.IsNull(source.Resolve(traversal));
    }

    [TestMethod]
    public void Candidate_outside_allowed_root_is_rejected()
    {
        using var root = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();

        Assert.ThrowsExactly<ArgumentException>(() => new AssemblyCandidate(
            Path.Combine(outside.Path, "Outside.Root.Component.dll"),
            root.Path));
    }

    [TestMethod]
    public void Candidate_deconstructs_to_its_normalized_public_contract_values()
    {
        using var directory = new TemporaryDirectory();
        var candidate = new AssemblyCandidate(
            Path.Combine(directory.Path, ".", "Component.dll"),
            Path.Combine(directory.Path, "."));

        var (path, root) = candidate;

        Assert.AreEqual(Path.Combine(directory.Path, "Component.dll"), path);
        Assert.AreEqual(directory.Path, root);
    }

    [TestMethod]
    public void Directory_source_returns_null_for_a_malformed_matching_dll()
    {
        using var directory = new TemporaryDirectory();
        var identity = new AssemblyName("Malformed.Component");
        File.WriteAllText(Path.Combine(directory.Path, "Malformed.Component.dll"), "not a managed assembly");
        var source = new DirectoryAssemblySource(directory.Path);

        Assert.IsNull(source.Resolve(identity));
    }

    [TestMethod]
    public void Manifest_source_construction_does_not_load_an_assembly()
    {
        using var directory = new TemporaryDirectory();
        var path = CopyAssembly(directory.Path, typeof(ManagedAssemblySourceTests).Assembly);
        var before = AppDomain.CurrentDomain.GetAssemblies().Length;

        _ = new ManifestAssemblySource(
        [
            (typeof(ManagedAssemblySourceTests).Assembly.GetName(), new AssemblyCandidate(path, directory.Path)),
        ]);

        Assert.AreEqual(before, AppDomain.CurrentDomain.GetAssemblies().Length);
    }

    static string CopyAssembly(string directory, Assembly assembly, string? name = null)
    {
        var destination = Path.Combine(directory, name ?? Path.GetFileName(assembly.Location));
        File.Copy(assembly.Location, destination);
        return destination;
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"assembly-isolation-{Guid.NewGuid():N}");
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
