using System.Reflection;
using System.Runtime.InteropServices;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Sources;

namespace DevTools.AssemblyIsolation.NetFramework.Tests;

public sealed class ScopedNetFrameworkSessionTests
{
    [Fact]
    public void Scoped_session_resolves_only_while_its_assembly_resolve_handler_is_registered()
    {
        Assert.Equal(4, Environment.Version.Major);
        Assert.Contains(".NET Framework", RuntimeInformation.FrameworkDescription, StringComparison.Ordinal);

        using var workload = FixtureWorkload.Create();
        var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(workload.EntryPath)
                .WithKind(AssemblyIsolationKind.Isolated)
                .AddManagedSource(new DirectoryAssemblySource(workload.Directory)));
        var entry = session.LoadEntryAssembly();
        var entryType = entry.GetType("IsolationEntry.Entry", throwOnError: true)!;
        var loadDependency = entryType.GetMethod("GetPrivateDependencyName", BindingFlags.Public | BindingFlags.Static)!;
        var loadAfterDisposeDependency = entryType.GetMethod("GetAfterDisposeDependencyName", BindingFlags.Public | BindingFlags.Static)!;

        var activeResult = (string)loadDependency.Invoke(null, null)!;

        Assert.Equal("System.Private.IsolationFixture", new AssemblyName(activeResult).Name);

        session.Dispose();
        session.Dispose();

        var failure = Assert.Throws<TargetInvocationException>(() => loadAfterDisposeDependency.Invoke(null, null));
        Assert.IsType<FileNotFoundException>(failure.InnerException);
    }

    [Fact]
    public void Isolated_session_does_not_claim_to_unload_default_app_domain_assemblies()
    {
        var entry = typeof(ScopedNetFrameworkSessionTests).Assembly;
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(entry.Location)
                .WithKind(AssemblyIsolationKind.Isolated)
                .Pin(entry));

        Assert.Same(entry, session.LoadEntryAssembly());

        var result = session.VerifyUnload();

        Assert.False(result.IsCollectible);
        Assert.False(result.IsUnloaded);
        Assert.NotNull(result.Detail);
    }

    [Fact]
    public void Scoped_session_rejects_a_managed_candidate_that_escapes_through_a_child_link()
    {
        using var fixture = FixtureWorkload.Create();
        using var reparsePoint = ReparsePointWorkload.Create();
        var candidate = new AssemblyCandidate(reparsePoint.LinkedCandidatePath, reparsePoint.AllowedRoot);
        var diagnostics = new RecordingDiagnosticSink();
        using var session = AssemblyIsolationSession.Create(
            AssemblyIsolationPlan.Create(fixture.EntryPath)
                .WithKind(AssemblyIsolationKind.Isolated)
                .AddManagedSource(new FixedManagedSource(candidate))
                .WithDiagnosticSink(diagnostics));
        var entry = session.LoadEntryAssembly();
        var loadDependency = entry.GetType("IsolationEntry.Entry", throwOnError: true)!
            .GetMethod("GetAfterDisposeDependencyName", BindingFlags.Public | BindingFlags.Static)!;

        Assert.True(ReparsePointWorkload.IsLexicallyUnderRoot(candidate.Path, candidate.Root));
        Assert.True(File.Exists(candidate.Path), candidate.Path);

        var failure = Assert.Throws<TargetInvocationException>(() => loadDependency.Invoke(null, null));

        Assert.IsType<FileNotFoundException>(failure.InnerException);
        var diagnostic = Assert.Single(
            diagnostics.Diagnostics,
            item => item.Code == "managed-candidate-rejected"
                    && string.Equals(item.RequestedAssembly?.Name, "System.Private.AfterDisposeFixture", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(candidate.Path, diagnostic.Message, StringComparison.Ordinal);
        Assert.Contains("outside its root", diagnostic.Message, StringComparison.Ordinal);
    }

    sealed class FixedManagedSource : IManagedAssemblySource
    {
        readonly AssemblyCandidate candidate;

        public FixedManagedSource(AssemblyCandidate candidate) => this.candidate = candidate;

        public AssemblyCandidate? Resolve(AssemblyName requested) => candidate;
    }

    sealed class RecordingDiagnosticSink : IAssemblyIsolationDiagnosticSink
    {
        public List<AssemblyIsolationDiagnostic> Diagnostics { get; } = new List<AssemblyIsolationDiagnostic>();

        public void Publish(AssemblyIsolationDiagnostic diagnostic) => Diagnostics.Add(diagnostic);
    }
}

sealed class FixtureWorkload : IDisposable
{
    FixtureWorkload(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    public string EntryPath => Path.Combine(Directory, "IsolationEntry.dll");

    public static FixtureWorkload Create()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DevTools.AssemblyIsolation.NetFramework.Tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        CopyFixture("IsolationEntry", "IsolationEntry.dll", directory);
        CopyFixture("PrivateSystemNamedDependency", "System.Private.IsolationFixture.dll", directory);
        CopyFixture("PrivateAfterDisposeDependency", "System.Private.AfterDisposeFixture.dll", directory);
        return new FixtureWorkload(directory);
    }

    public void Dispose()
    {
        TryDeleteLoadedDirectory(Directory);
    }

    static void CopyFixture(string projectName, string assemblyName, string directory)
    {
        var source = Path.Combine(FindRepositoryRoot(), "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", projectName, "bin", "Debug", "net48", assemblyName);
        File.Copy(source, Path.Combine(directory, assemblyName));
    }

    internal static void TryDeleteLoadedDirectory(string directory)
    {
        try
        {
            if (System.IO.Directory.Exists(directory))
                System.IO.Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // net48 LoadFile keeps the shadow DLL mapped until process exit.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    static string FindRepositoryRoot()
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
        var parent = Path.Combine(Path.GetTempPath(), "DevTools.AssemblyIsolation.NetFramework.ReparsePointTests", Guid.NewGuid().ToString("N"));
        var allowedRoot = Path.Combine(parent, "allowed");
        var externalDirectory = Path.Combine(parent, "external");
        var linkPath = Path.Combine(allowedRoot, "linked");
        Directory.CreateDirectory(allowedRoot);
        Directory.CreateDirectory(externalDirectory);
        var source = Path.Combine(FindRepositoryRoot(), "tests", "DevTools.AssemblyIsolation.Tests", "Fixtures", "PrivateAfterDisposeDependency", "bin", "Debug", "net48", "System.Private.AfterDisposeFixture.dll");
        File.Copy(source, Path.Combine(externalDirectory, "candidate.dll"));
        if (!CreateSymbolicLink(linkPath, externalDirectory, DirectoryLink | AllowUnprivilegedCreate))
            throw new IOException("Could not create the reparse-point test link.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));

        return new ReparsePointWorkload(allowedRoot, externalDirectory, linkPath, Path.Combine(linkPath, "candidate.dll"));
    }

    public static bool IsLexicallyUnderRoot(string path, string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
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

    static string FindRepositoryRoot()
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

    const int DirectoryLink = 1;
    const int AllowUnprivilegedCreate = 2;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateSymbolicLink(string linkPath, string targetPath, int flags);
}
