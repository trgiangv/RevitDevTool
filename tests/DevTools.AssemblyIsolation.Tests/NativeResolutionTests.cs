using System.Reflection;
using System.Runtime.CompilerServices;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.Tests;

public sealed class NativeResolutionTests
{
    [Fact]
    public void Collectible_session_rejects_an_out_of_root_native_candidate_without_loading_it()
    {
        using var allowedRoot = new TemporaryDirectory();
        var candidate = CreateEscapingCandidate(typeof(NativeResolutionTests).Assembly.Location, allowedRoot.Path);
        var diagnostics = new RecordingDiagnosticSink();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(typeof(NativeResolutionTests).Assembly.Location)
                .WithKind(AssemblyIsolationKind.Collectible)
                .AddNativeSource(new FixedNativeSource(candidate))
                .WithDiagnosticSink(diagnostics));

        var handle = session.ResolveNativeForTesting("out-of-root-native");

        Assert.Equal(nint.Zero, handle);
        var diagnostic = Assert.Single(diagnostics.Diagnostics);
        Assert.Equal("native-candidate-rejected", diagnostic.Code);
        Assert.Contains("out-of-root-native", diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains(candidate.Path, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("outside its root", diagnostic.Message, StringComparison.Ordinal);
    }

    static AssemblyCandidate CreateEscapingCandidate(string path, string allowedRoot)
    {
        var candidate = (AssemblyCandidate)RuntimeHelpers.GetUninitializedObject(typeof(AssemblyCandidate));
        SetAutoProperty(candidate, "Path", path);
        SetAutoProperty(candidate, "Root", allowedRoot);
        return candidate;
    }

    static void SetAutoProperty(AssemblyCandidate candidate, string name, string value)
    {
        var field = typeof(AssemblyCandidate).GetField($"<{name}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(typeof(AssemblyCandidate).FullName, name);
        field.SetValue(candidate, value);
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

    sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "assembly-isolation-native", Guid.NewGuid().ToString("N"));
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
