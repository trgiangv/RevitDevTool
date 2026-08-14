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
        Assert.Contains("DevToolsNUnitRepack", props, StringComparison.Ordinal);
        Assert.Contains("ILRepack", props, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRepackable", props, StringComparison.Ordinal);
    }

    [Fact]
    public void Net48_consumer_targets_ilrepack_test_exe_not_nunit_or_host_apis()
    {
        var buildDir = Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "build");
        var targets = File.ReadAllText(Path.Combine(buildDir, "RevitDevTool.NUnit.targets"));
        var ilrepack = File.ReadAllText(Path.Combine(buildDir, "ILRepack.targets"));
        var csproj = File.ReadAllText(Path.Combine(
            RepositoryRoot,
            "source",
            "DevTools.NUnit.Mtp",
            "DevTools.NUnit.Mtp.csproj"));

        Assert.Contains("ILRepack.targets", targets, StringComparison.Ordinal);
        Assert.Contains("build\\ILRepack.targets", csproj, StringComparison.Ordinal);
        Assert.Contains("PkgILRepack", csproj, StringComparison.Ordinal);
        Assert.Contains("PackDevToolsNUnitILRepackTool", csproj, StringComparison.Ordinal);
        Assert.Contains("PackagePath=\"tools/%(Filename)%(Extension)\"", csproj, StringComparison.Ordinal);
        Assert.Contains("/internalize", ilrepack, StringComparison.Ordinal);
        Assert.Contains("$(TargetPath)", ilrepack, StringComparison.Ordinal);
        Assert.Contains("nunit.framework.dll", ilrepack, StringComparison.Ordinal);
        Assert.Contains("Autodesk.*.dll", ilrepack, StringComparison.Ordinal);
        Assert.Contains("MahApps", ilrepack, StringComparison.Ordinal);
        Assert.Contains("StartsWith('net4')", ilrepack, StringComparison.Ordinal);
        Assert.Contains("DevToolsNUnitRepack", ilrepack, StringComparison.Ordinal);
        Assert.DoesNotContain("IsRepackable", ilrepack, StringComparison.Ordinal);
    }
}
