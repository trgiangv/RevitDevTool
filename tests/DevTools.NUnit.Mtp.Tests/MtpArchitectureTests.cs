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
    }
}
