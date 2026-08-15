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
        Assert.Contains("EnsureSession()", framework, StringComparison.Ordinal);
        Assert.Contains("PublishRunAsync(EnsureSession()", framework, StringComparison.Ordinal);
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
        Assert.DoesNotContain("IsRepackable", props, StringComparison.Ordinal);
    }

    [Fact]
    public void Net48_mtp_ilrepacks_own_dll_not_consumer_exe()
    {
        var mtpDir = Path.Combine(RepositoryRoot, "source", "DevTools.NUnit.Mtp");
        var targets = File.ReadAllText(Path.Combine(mtpDir, "build", "RevitDevTool.NUnit.targets"));
        var csproj = File.ReadAllText(Path.Combine(mtpDir, "DevTools.NUnit.Mtp.csproj"));

        Assert.DoesNotContain("ILRepack", targets, StringComparison.Ordinal);
        Assert.DoesNotContain("ILRepack.Lib.MSBuild.Task", csproj, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"ILRepack\"", csproj, StringComparison.Ordinal);
        Assert.Contains("IsRepackable", csproj, StringComparison.Ordinal);
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
            "DevTools.NUnit.Runner",
            "Debugging",
            "VisualStudioAttach.cs");
        var attach = File.ReadAllText(debugging);
        Assert.Contains("EnvDTE", attach, StringComparison.Ordinal);
        Assert.Contains("DebuggedProcesses", attach, StringComparison.Ordinal);

        var runnerCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Runner",
            "DevTools.NUnit.Runner.csproj"));
        Assert.Contains("Microsoft.VisualStudio.Interop", runnerCsproj, StringComparison.Ordinal);

        var mtpCsproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "DevTools.NUnit.Mtp.csproj"));
        Assert.DoesNotContain("Microsoft.VisualStudio.Interop", mtpCsproj, StringComparison.Ordinal);
    }
}
