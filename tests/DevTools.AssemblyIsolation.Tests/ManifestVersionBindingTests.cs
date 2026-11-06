using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;
using System.Reflection;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class ManifestVersionBindingTests
{
    private const string Token = "cc7b13ffcd2ddd51";

    [TestMethod]
    [DataRow("1.0.0.0", "2.0.0.0")]
    [DataRow("2.0.0.0", "1.0.0.0")]
    public void Default_source_stays_exact(string requestedVersion, string fileVersion)
    {
        using var directory = new TemporaryDirectory();
        var source = SourceWith(directory, fileVersion, bindsAnyVersion: false);

        Assert.IsFalse(source.BindsAnyVersion);
        Assert.IsNull(source.Resolve(Identity(requestedVersion)));
    }

    [TestMethod]
    [DataRow("1.0.0.0", "2.0.0.0")]
    [DataRow("2.0.0.0", "1.0.0.0")]
    public void Any_version_source_binds_the_listed_file(string requestedVersion, string fileVersion)
    {
        using var directory = new TemporaryDirectory();
        var source = SourceWith(directory, fileVersion, bindsAnyVersion: true);

        Assert.AreEqual(Path.Combine(directory.Path, "Contoso.Component.dll"), source.Resolve(Identity(requestedVersion))!.Path);
    }

    [TestMethod]
    public void Any_version_source_prefers_the_exact_file_then_the_newest()
    {
        using var directory = new TemporaryDirectory();
        var oldPath = Path.Combine(directory.Path, "old.dll");
        var newPath = Path.Combine(directory.Path, "new.dll");
        var source = new ManifestAssemblySource(
            [
                (Identity("1.0.0.0"), new AssemblyCandidate(oldPath, directory.Path)),
                (Identity("3.0.0.0"), new AssemblyCandidate(newPath, directory.Path)),
            ],
            bindsAnyVersion: true);

        Assert.AreEqual(oldPath, source.Resolve(Identity("1.0.0.0"))!.Path);
        Assert.AreEqual(newPath, source.Resolve(Identity("2.0.0.0"))!.Path);
    }

    [TestMethod]
    public void Any_version_source_still_requires_the_public_key_token()
    {
        using var directory = new TemporaryDirectory();
        var source = SourceWith(directory, "2.0.0.0", bindsAnyVersion: true);
        var otherToken = new AssemblyName("Contoso.Component, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");

        Assert.IsNull(source.Resolve(otherToken));
    }

    [TestMethod]
    [DataRow("SameSimpleNameV1", "2.0.0.0")]
    [DataRow("SameSimpleNameV2", "1.0.0.0")]
    public void Session_loads_the_listed_file_for_a_request_of_another_version(string fixture, string requestedVersion)
    {
        using var directory = new TemporaryDirectory();
        var path = MetadataAssemblySessionTests.CopyFixture(fixture, "SameSimpleName.dll", directory.Path);
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(typeof(ManifestVersionBindingTests).Assembly.Location)
                .WithKind(AssemblyIsolationKind.Collectible)
                .AddManagedSource(new ManifestAssemblySource(
                    [new AssemblyCandidate(path, directory.Path)],
                    bindsAnyVersion: true)));

        var loaded = session.ResolveManagedForTesting(new AssemblyName($"SameSimpleName, Version={requestedVersion}"));

        Assert.IsNotNull(loaded);
        Assert.AreEqual(AssemblyName.GetAssemblyName(path).Version, loaded.GetName().Version);
    }

    [TestMethod]
    [DataRow("SameSimpleNameV1", "2.0.0.0")]
    [DataRow("SameSimpleNameV2", "1.0.0.0")]
    public void Default_session_rejects_another_version_and_publishes_why(string fixture, string requestedVersion)
    {
        using var directory = new TemporaryDirectory();
        var path = MetadataAssemblySessionTests.CopyFixture(fixture, "SameSimpleName.dll", directory.Path);
        var diagnostics = new RecordingDiagnosticSink();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(typeof(ManifestVersionBindingTests).Assembly.Location)
                .WithKind(AssemblyIsolationKind.Collectible)
                .AddManagedSource(new ManifestAssemblySource([new AssemblyCandidate(path, directory.Path)]))
                .WithDiagnosticSink(diagnostics));

        Assert.IsNull(session.ResolveManagedForTesting(new AssemblyName($"SameSimpleName, Version={requestedVersion}")));

        var diagnostic = Assert.ContainsSingle(diagnostics.Diagnostics);
        Assert.AreEqual("managed-candidate-rejected", diagnostic.Code);
        Assert.Contains(path, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(AssemblyName.GetAssemblyName(path).FullName, diagnostic.Message, StringComparison.Ordinal);
    }

    private static ManifestAssemblySource SourceWith(TemporaryDirectory directory, string version, bool bindsAnyVersion) =>
        new(
            [(Identity(version), new AssemblyCandidate(Path.Combine(directory.Path, "Contoso.Component.dll"), directory.Path))],
            bindsAnyVersion);

    private static AssemblyName Identity(string version) =>
        new($"Contoso.Component, Version={version}, Culture=neutral, PublicKeyToken={Token}");

    private sealed class RecordingDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = [];

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }

    private sealed class TemporaryDirectory : IDisposable
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
