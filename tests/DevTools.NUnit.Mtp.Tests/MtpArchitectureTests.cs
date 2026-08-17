namespace DevTools.NUnit.Mtp.Tests;

public sealed class MtpArchitectureTests
{
    private static readonly string RepositoryRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    [Fact]
    public void Mtp_DoesNotLocateOrLaunchAutodeskHosts()
    {
        var directory = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Mtp");
        var forbidden = new[]
        {
            "HostLocator",
            "HostSession",
            "Revit.exe",
            "acad.exe",
            "Microsoft.Win32.Registry",
            "EnvDTE",
            "Microsoft.VisualStudio.Interop",
            "GetActiveObject",
            "VisualStudio.DTE",
        };

        var offenders = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Select(path => (path, content: File.ReadAllText(path)))
            .SelectMany(file => forbidden
                .Where(pattern => file.content.Contains(pattern, StringComparison.Ordinal))
                .Select(pattern => $"{Path.GetRelativePath(RepositoryRoot, file.path)} -> {pattern}"))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Mtp_discovery_does_not_invoke_host_runner()
    {
        var framework = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "DevToolsNUnitFramework.cs"));

        Assert.Contains("NUnitMetadataDiscoverer.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("session.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("_transport.Discover", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("IDebugSession", framework, StringComparison.Ordinal);
        Assert.DoesNotContain("SystemDebugSession", framework, StringComparison.Ordinal);
        Assert.Contains("Debugger.IsAttached", framework, StringComparison.Ordinal);
        Assert.Contains("EnsureSession()", framework, StringComparison.Ordinal);
        Assert.Contains("PublishRunAsync(EnsureSession()", framework, StringComparison.Ordinal);
        Assert.Contains("ApplyDebugParent", framework, StringComparison.Ordinal);

        var session = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "DevToolsNUnitSession.cs"));
        Assert.DoesNotContain("Discover(", session, StringComparison.Ordinal);

        var client = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Core",
            "Client",
            "ProcessRunnerClient.cs"));
        Assert.DoesNotContain("NUnitRunnerCli.DiscoverCommand", client, StringComparison.Ordinal);
        Assert.DoesNotContain("IReadOnlyList<NUnitDiscoveredTest> Discover", client, StringComparison.Ordinal);

        var transport = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Core",
            "Client",
            "IRunnerTransport.cs"));
        Assert.DoesNotContain("Discover(", transport, StringComparison.Ordinal);
    }

    [Fact]
    public void Mtp_and_vstest_share_the_runner_client_via_linked_core_files()
    {
        var client = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Core", "Client", "ProcessRunnerClient.cs");
        Assert.True(File.Exists(client));

        var coreCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot, "source", "DevTools.NUnit.Core", "DevTools.NUnit.Core.csproj"));
        Assert.Contains("Compile Remove=\"Client\\**\"", coreCsproj, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(
            RepositoryRoot, "source", "DevTools.NUnit.Mtp", "ProcessRunnerClient.cs")));
        Assert.False(Directory.Exists(Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Client")));
    }

    [Fact]
    public void Net48_consumer_props_enable_binding_redirects()
    {
        var props = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "build",
            "RevitDevTool.NUnit.props"));

        Assert.Contains("GenerateBindingRedirectsOutputType", props, StringComparison.Ordinal);
        Assert.Contains("System.Runtime.CompilerServices.Unsafe", props, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack", props, StringComparison.Ordinal);
        Assert.DoesNotContain("DevToolsNUnitRepack", props, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepackable", props, StringComparison.Ordinal);
    }

    [Fact]
    public void Net48_mtp_ilrepacks_own_dll_not_consumer_exe()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Mtp");
        var targets = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.NUnit.targets"));
        var csproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.NUnit.Mtp.csproj"));
        var ilRepackTargets = File.ReadAllText(Path.Combine(RepositoryRoot, "props", "ILRepack.targets"));

        Assert.DoesNotContain("ILRepack", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack.Lib.MSBuild.Task", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("<PackageReference Include=\"ILRepack\"", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"ILRepack\"", ilRepackTargets, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRepackable", ilRepackTargets, StringComparison.Ordinal);
        Assert.Contains("ILRepackable", csproj, StringComparison.Ordinal);
        Assert.Contains("ILRepackInternalize", csproj, StringComparison.Ordinal);
        Assert.Contains("'$(TargetFramework)' == 'net48'", csproj, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(RepositoryRoot, "props", "ILRepack.targets")));
        Assert.False(File.Exists(Path.Combine(mtpDir, "ILRepack.targets")));
    }

    [Fact]
    public void Runner_owns_visual_studio_interop()
    {
        var debugging = Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestRunner",
            "Debugging",
            "VisualStudioAttach.cs");
        var attach = File.ReadAllText(debugging);
        Assert.Contains("EnvDTE", attach, StringComparison.Ordinal);
        Assert.Contains("DebuggedProcesses", attach, StringComparison.Ordinal);

        var runnerCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.TestRunner",
            "DevTools.TestRunner.csproj"));
        Assert.Contains("Microsoft.VisualStudio.Interop", runnerCsproj, StringComparison.Ordinal);

        var mtpCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "DevTools.NUnit.Mtp.csproj"));
        Assert.DoesNotContain("Microsoft.VisualStudio.Interop", mtpCsproj, StringComparison.Ordinal);
    }
}
