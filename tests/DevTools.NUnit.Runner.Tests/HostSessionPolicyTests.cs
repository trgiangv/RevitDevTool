namespace DevTools.NUnit.Runner.Tests;

public sealed class HostSessionPolicyTests
{
    [Fact]
    public void HostLaunch_false_reuses_matching_host_then_falls_back_to_spawn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "source",
            "DevTools.NUnit.Runner",
            "Services",
            "HostSession.cs"));

        Assert.Contains("reuses a matching-version instance when one is already running", source, StringComparison.Ordinal);
        Assert.Contains("otherwise starts a new host", source, StringComparison.Ordinal);
        Assert.Contains("always starts a new host", source, StringComparison.Ordinal);

        var reuseBlockStart = source.IndexOf("if (!forceLaunch)", StringComparison.Ordinal);
        var launchCall = source.IndexOf("launchService.Start", StringComparison.Ordinal);
        Assert.True(reuseBlockStart >= 0 && launchCall > reuseBlockStart);
        var reuseBlock = source[reuseBlockStart..launchCall];
        Assert.Contains("HostLocator.Discover", reuseBlock, StringComparison.Ordinal);
        Assert.Contains("return existing", reuseBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("throw new InvalidOperationException", reuseBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("launchService.Start", reuseBlock, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
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
