using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Identity;
using DevTools.AssemblyIsolation.Sources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Threading.Tasks;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

[TestClass]
public sealed class NetfxClosureBindTests
{
    [TestMethod]
    public void Allows_newer_unifies_stj_nine_onto_ten()
    {
        var requested = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsFalse(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.IsTrue(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [TestMethod]
    public void Allows_newer_does_not_downgrade_stj()
    {
        var requested = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsFalse(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [TestMethod]
    public void Allows_newer_unifies_tasks_extensions_compile_ref_onto_package_identity()
    {
        var requested = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsFalse(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.IsTrue(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [TestMethod]
    public void Allows_newer_does_not_downgrade_tasks_extensions()
    {
        var requested = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsFalse(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [TestMethod]
    public void Allows_newer_unifies_any_closure_simple_name()
    {
        var requested = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsFalse(AssemblyIdentityMatcher.IsCompatible(requested, candidate));
        Assert.IsTrue(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [TestMethod]
    public void Allows_newer_does_not_unify_token_mismatch()
    {
        var requested = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var candidate = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

        Assert.IsFalse(NetfxClosureBind.AllowsNewer(requested, candidate));
    }

    [TestMethod]
    public void Try_find_loaded_does_not_scan_the_default_domain()
    {
        var requested = new AssemblyName(
            "Microsoft.Bcl.AsyncInterfaces, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsFalse(NetfxClosureBind.TryFindLoaded(requested, [], out _));
    }

    [TestMethod]
    public void Try_find_loaded_reuses_only_supplied_tasks_extensions()
    {
        var loaded = typeof(ValueTask).Assembly;
        if (!string.Equals(loaded.GetName().Name, "System.Threading.Tasks.Extensions", StringComparison.Ordinal))
            Assert.Inconclusive("ValueTask is not System.Threading.Tasks.Extensions in this testhost.");

        var requested = new AssemblyName(
            "System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");

        Assert.IsTrue(NetfxClosureBind.TryFindLoaded(requested, [loaded], out var actual));
        Assert.AreSame(loaded, actual);
        Assert.IsFalse(NetfxClosureBind.TryFindLoaded(requested, [], out _));
    }

    [TestMethod]
    public void Manifest_unifies_stj_nine_request_onto_ten_when_nine_is_absent()
    {
        using var directory = new TemporaryDirectory();
        var newerPath = Path.Combine(directory.Path, "System.Text.Json.dll");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.AreEqual(Path.GetFullPath(newerPath), source.Resolve(olderIdentity)!.Path);
    }

    [TestMethod]
    public void Manifest_unifies_tasks_extensions_compile_ref_when_older_is_absent()
    {
        using var directory = new TemporaryDirectory();
        var newerPath = Path.Combine(directory.Path, "System.Threading.Tasks.Extensions.dll");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.1.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Threading.Tasks.Extensions, Version=4.2.4.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.AreEqual(Path.GetFullPath(newerPath), source.Resolve(olderIdentity)!.Path);
    }

    [TestMethod]
    public void Manifest_unifies_arbitrary_compile_ref_when_older_is_absent()
    {
        using var directory = new TemporaryDirectory();
        var newerPath = Path.Combine(directory.Path, "Contoso.Component.dll");
        File.WriteAllText(newerPath, "placeholder");

        var olderIdentity = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (newerIdentity, new AssemblyCandidate(newerPath, directory.Path)),
        ]);

        Assert.AreEqual(Path.GetFullPath(newerPath), source.Resolve(olderIdentity)!.Path);
    }

    [TestMethod]
    public void Manifest_does_not_downgrade_stj()
    {
        using var directory = new TemporaryDirectory();
        var olderPath = Path.Combine(directory.Path, "System.Text.Json.dll");
        File.WriteAllText(olderPath, "placeholder");

        var olderIdentity = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
        [
            (olderIdentity, new AssemblyCandidate(olderPath, directory.Path)),
        ]);

        Assert.IsNull(source.Resolve(newerIdentity));
    }

    [TestMethod]
    public void Any_version_manifest_binds_a_request_for_a_newer_version_to_the_listed_file()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "System.Text.Json.dll");
        File.WriteAllText(path, "placeholder");

        var olderIdentity = new AssemblyName("System.Text.Json, Version=9.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var newerIdentity = new AssemblyName("System.Text.Json, Version=10.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var source = new ManifestAssemblySource(
            [(olderIdentity, new AssemblyCandidate(path, directory.Path))],
            bindsAnyVersion: true);

        Assert.AreEqual(Path.GetFullPath(path), source.Resolve(newerIdentity)!.Path);
    }

    [TestMethod]
    public void Any_version_manifest_still_requires_the_public_key_token()
    {
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "Contoso.Component.dll");
        File.WriteAllText(path, "placeholder");

        var listed = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51");
        var requested = new AssemblyName("Contoso.Component, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
        var source = new ManifestAssemblySource(
            [(listed, new AssemblyCandidate(path, directory.Path))],
            bindsAnyVersion: true);

        Assert.IsNull(source.Resolve(requested));
    }

    [TestMethod]
    [DataRow("2.0.0.0", "1.0.0.0")]
    [DataRow("1.0.0.0", "2.0.0.0")]
    public void Any_version_session_binds_a_real_reference_to_the_listed_file(string compiledAgainst, string listed)
    {
        using var directory = new TemporaryDirectory();
        var library = UniqueLibraryName();
        var entryPath = WriteEntryCompiledAgainstLibrary(directory.Path, library, compiledAgainst, listed);
        var libraryPath = Path.Combine(directory.Path, library + ".dll");
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(entryPath)
                .WithKind(AssemblyIsolationKind.Isolated)
                .WithDistinctFileIdentity()
                .AddManagedSource(new ManifestAssemblySource(
                    [new AssemblyCandidate(libraryPath, directory.Path)],
                    bindsAnyVersion: true)));

        var value = session.LoadEntryAssembly().GetType("Fixture.Entry", throwOnError: true)!
            .GetMethod("Value", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(null, null);

        Assert.AreEqual(listed, value);
    }

    [TestMethod]
    public void Default_session_rejects_a_lower_file_and_publishes_why()
    {
        using var directory = new TemporaryDirectory();
        var library = UniqueLibraryName();
        var entryPath = WriteEntryCompiledAgainstLibrary(directory.Path, library, compiledAgainst: "2.0.0.0", listed: "1.0.0.0");
        var libraryPath = Path.Combine(directory.Path, library + ".dll");
        var diagnostics = new RecordingDiagnosticSink();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(entryPath)
                .WithKind(AssemblyIsolationKind.Isolated)
                .WithDistinctFileIdentity()
                .AddManagedSource(new ManifestAssemblySource([new AssemblyCandidate(libraryPath, directory.Path)]))
                .WithDiagnosticSink(diagnostics));

        var method = session.LoadEntryAssembly().GetType("Fixture.Entry", throwOnError: true)!
            .GetMethod("Value", BindingFlags.Public | BindingFlags.Static)!;

        var error = Assert.ThrowsExactly<TargetInvocationException>(() => method.Invoke(null, null));

        var missing = Assert.IsInstanceOfType<FileNotFoundException>(error.InnerException);
        Assert.Contains(library, missing.FileName!, StringComparison.Ordinal);
        Assert.IsFalse(AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == library));
        Assert.IsTrue(diagnostics.Diagnostics.Any(diagnostic =>
            diagnostic.Code == "managed-candidate-rejected"
            && diagnostic.Message.Contains(libraryPath)));
    }

    [TestMethod]
    public void Session_answers_a_shared_assembly_only_when_it_shares_with_its_closure()
    {
        using var directory = new TemporaryDirectory();
        var library = UniqueLibraryName();
        var entryPath = WriteEntryCompiledAgainstLibrary(directory.Path, library, "1.0.0.0", "1.0.0.0");
        var libraryPath = Path.Combine(directory.Path, library + ".dll");
        var shared = Assembly.Load(File.ReadAllBytes(entryPath));
        var value = shared.GetType("Fixture.Entry", throwOnError: true)!
            .GetMethod("Value", BindingFlags.Public | BindingFlags.Static)!;

        AssemblyIsolationPlan Plan() => AssemblyIsolationPlan.Create(entryPath)
            .WithKind(AssemblyIsolationKind.Isolated)
            .WithDistinctFileIdentity()
            .AddManagedSource(new ManifestAssemblySource([new AssemblyCandidate(libraryPath, directory.Path)]));

        using (AssemblyIsolationSession.Create(Plan().Share(shared)))
        {
            var error = Assert.ThrowsExactly<TargetInvocationException>(() => value.Invoke(null, null));
            Assert.IsInstanceOfType<FileNotFoundException>(error.InnerException);
        }

        using (AssemblyIsolationSession.Create(Plan().ShareWithClosure(shared)))
        {
            Assert.AreEqual("1.0.0.0", value.Invoke(null, null));
        }
    }

    static string UniqueLibraryName() => $"VersionedLibrary{Guid.NewGuid():N}";

    static string WriteEntryCompiledAgainstLibrary(string directory, string library, string compiledAgainst, string listed)
    {
        var reference = Path.Combine(directory, "reference");
        Directory.CreateDirectory(reference);
        var referencePath = Path.Combine(reference, library + ".dll");
        Emit(referencePath, library, compiledAgainst, LibrarySource(compiledAgainst));
        Emit(Path.Combine(directory, library + ".dll"), library, listed, LibrarySource(listed));

        var entryPath = Path.Combine(directory, "VersionedEntry.dll");
        Emit(
            entryPath,
            "VersionedEntry",
            "1.0.0.0",
            "namespace Fixture { public static class Entry { public static string Value() => Library.Library.Version(); } }",
            referencePath);
        return entryPath;
    }

    static string LibrarySource(string version) =>
        "namespace Library { public static class Library { public static string Version() { return \"" + version + "\"; } } }";

    static void Emit(string path, string name, string version, string source, params string[] references)
    {
        var compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText($"[assembly:System.Reflection.AssemblyVersion(\"{version}\")] {source}")],
            references.Append(typeof(object).Assembly.Location).Select(reference => MetadataReference.CreateFromFile(reference)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using var stream = File.Create(path);
        var result = compilation.Emit(stream);
        if (!result.Success)
            throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
    }

    sealed class RecordingDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = [];

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"assembly-isolation-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => FixtureWorkload.TryDeleteLoadedDirectory(Path);
    }
}
