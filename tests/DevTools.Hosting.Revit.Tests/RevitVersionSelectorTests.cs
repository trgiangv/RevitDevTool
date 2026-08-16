using DevTools.Hosting;
using DevTools.Hosting.Revit;

namespace DevTools.Hosting.Revit.Tests;

public sealed class RevitVersionSelectorTests
{
    [Fact]
    public void File_year_is_a_minimum_oldest_installed_greater_or_equal_wins()
    {
        var selected = RevitVersionSelector.FindCompatibleVersion("2025", ["2026"]);
        Assert.Equal("2026", selected);
    }

    [Fact]
    public void Oldest_compatible_is_chosen_when_several_are_installed()
    {
        var selected = RevitVersionSelector.FindCompatibleVersion("2025", ["2027", "2025", "2026"]);
        Assert.Equal("2025", selected);
    }

    [Fact]
    public void Missing_document_year_picks_newest_installed()
    {
        var selected = RevitVersionSelector.FindCompatibleVersion(null, ["2024", "2026"]);
        Assert.Equal("2026", selected);
    }
}

public sealed class RevitFileAwareHostLaunchServiceTests
{
    [Fact]
    public void Explicit_version_skips_metadata()
    {
        var inner = new CapturingLaunchService();
        var readerCalled = false;
        var decorator = new RevitFileAwareHostLaunchService(
            inner,
            new StubPathResolver(["2025", "2026"]),
            _ =>
            {
                readerCalled = true;
                return "2025";
            });

        decorator.Start(
            new HostLaunchRequest(HostApp.Revit, "2026", @"C:\missing-is-ok-for-skip.rvt", null),
            TestContext.Current.CancellationToken);

        Assert.False(readerCalled);
        Assert.Equal("2026", inner.LastRequest?.Version);
    }

    [Fact]
    public void File_2025_with_only_2026_installed_selects_2026()
    {
        var inner = new CapturingLaunchService();
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".rvt");
        File.WriteAllText(temp, "stub");
        try
        {
            var decorator = new RevitFileAwareHostLaunchService(
                inner,
                new StubPathResolver(["2026"]),
                _ => "2025");

            decorator.Start(
                new HostLaunchRequest(HostApp.Revit, "", temp, null),
                TestContext.Current.CancellationToken);

            Assert.Equal("2026", inner.LastRequest?.Version);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    private sealed class CapturingLaunchService : IHostLaunchService
    {
        public HostLaunchRequest? LastRequest { get; private set; }

        public HostProcessStart Start(HostLaunchRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return null!;
        }
    }

    private sealed class StubPathResolver(IReadOnlyList<string> installed) : IHostPathResolver
    {
        public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;
        public string? FindExecutable(HostApp hostApp, string version) => @"C:\Revit.exe";
        public IReadOnlyList<string> GetInstalledVersions(HostApp hostApp) => installed;
    }
}
