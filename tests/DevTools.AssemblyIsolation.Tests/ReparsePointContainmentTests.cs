using System.Reflection;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class ReparsePointContainmentTests
{
    [Fact]
    public void Managed_candidate_through_a_child_link_is_rejected_even_though_its_lexical_path_is_under_the_root()
    {
        using var workload = ReparsePointWorkload.Create();
        var candidate = new AssemblyCandidate(workload.LinkedCandidatePath, workload.AllowedRoot);
        var diagnostics = new RecordingDiagnosticSink();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(typeof(ReparsePointContainmentTests).Assembly.Location)
                .WithKind(AssemblyIsolationKind.Collectible)
                .AddManagedSource(new FixedManagedSource(candidate))
                .WithDiagnosticSink(diagnostics));

        Assert.True(IsLexicallyUnderRoot(candidate.Path, candidate.Root));

        var resolved = session.ResolveManagedForTesting(typeof(ReparsePointContainmentTests).Assembly.GetName());

        Assert.Null(resolved);
        AssertRejected(diagnostics, "managed-candidate-rejected", candidate);
    }

    [Fact]
    public void Native_candidate_through_a_child_link_is_rejected_even_though_its_lexical_path_is_under_the_root()
    {
        using var workload = ReparsePointWorkload.Create();
        var candidate = new AssemblyCandidate(workload.LinkedCandidatePath, workload.AllowedRoot);
        var diagnostics = new RecordingDiagnosticSink();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(typeof(ReparsePointContainmentTests).Assembly.Location)
                .WithKind(AssemblyIsolationKind.Collectible)
                .AddNativeSource(new FixedNativeSource(candidate))
                .WithDiagnosticSink(diagnostics));

        Assert.True(IsLexicallyUnderRoot(candidate.Path, candidate.Root));

        Assert.Equal(nint.Zero, session.ResolveNativeForTesting("linked-native"));
        AssertRejected(diagnostics, "native-candidate-rejected", candidate);
    }

    static void AssertRejected(RecordingDiagnosticSink diagnostics, string code, AssemblyCandidate candidate)
    {
        var diagnostic = Assert.Single(diagnostics.Diagnostics, diagnostic => diagnostic.Code == code);
        Assert.Contains(candidate.Path, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("outside its root", diagnostic.Message, StringComparison.Ordinal);
    }

    static bool IsLexicallyUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    sealed class FixedManagedSource : IManagedAssemblySource
    {
        readonly AssemblyCandidate candidate;

        public FixedManagedSource(AssemblyCandidate candidate) => this.candidate = candidate;

        public AssemblyCandidate? Resolve(AssemblyName requested) => candidate;
    }

    sealed class FixedNativeSource : INativeAssemblySource
    {
        readonly AssemblyCandidate candidate;

        public FixedNativeSource(AssemblyCandidate candidate) => this.candidate = candidate;

        public AssemblyCandidate? Resolve(string name) => candidate;
    }

    sealed class RecordingDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = [];

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }
}

sealed class ReparsePointWorkload : IDisposable
{
    ReparsePointWorkload(string allowedRoot, string externalDirectory, string linkPath, string linkedCandidatePath)
    {
        AllowedRoot = allowedRoot;
        this.externalDirectory = externalDirectory;
        this.linkPath = linkPath;
        LinkedCandidatePath = linkedCandidatePath;
    }

    readonly string externalDirectory;
    readonly string linkPath;

    public string AllowedRoot { get; }

    public string LinkedCandidatePath { get; }

    public static ReparsePointWorkload Create()
    {
        var parent = Path.Combine(Path.GetTempPath(), "DevTools.AssemblyIsolation.ReparsePointTests", Guid.NewGuid().ToString("N"));
        var allowedRoot = Path.Combine(parent, "allowed");
        var externalDirectory = Path.Combine(parent, "external");
        var linkPath = Path.Combine(allowedRoot, "linked");
        Directory.CreateDirectory(allowedRoot);
        Directory.CreateDirectory(externalDirectory);

        var externalCandidatePath = Path.Combine(externalDirectory, "candidate.dll");
        File.Copy(typeof(ReparsePointContainmentTests).Assembly.Location, externalCandidatePath);
        Directory.CreateSymbolicLink(linkPath, externalDirectory);

        return new ReparsePointWorkload(
            allowedRoot,
            externalDirectory,
            linkPath,
            Path.Combine(linkPath, "candidate.dll"));
    }

    public void Dispose()
    {
        if (Directory.Exists(linkPath))
            Directory.Delete(linkPath);
        if (Directory.Exists(AllowedRoot))
            Directory.Delete(AllowedRoot);
        if (Directory.Exists(externalDirectory))
            Directory.Delete(externalDirectory, recursive: true);
        var parent = Directory.GetParent(AllowedRoot)?.FullName;
        if (parent is not null && Directory.Exists(parent))
            Directory.Delete(parent);
    }
}
