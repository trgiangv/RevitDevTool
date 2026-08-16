using DevTools.Hosting;
using DevTools.Hosting.Acad;
using DevTools.Hosting.Revit;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Hosting.Tests;

public sealed class HostLaunchSupportTests
{
    [Fact]
    public void SingleFor_returns_the_only_match()
    {
        var items = new[]
        {
            new FakeSupport(HostApp.Revit),
            new FakeSupport(HostApp.AutoCad),
        };

        var match = HostLaunchService.SingleFor(items, HostApp.Revit, item => item.Supports(HostApp.Revit));
        Assert.Same(items[0], match);
    }

    [Fact]
    public void SingleFor_throws_when_two_contracts_support_the_same_host()
    {
        var items = new[]
        {
            new FakeSupport(HostApp.Revit),
            new FakeSupport(HostApp.Revit),
        };

        Assert.Throws<InvalidOperationException>(
            () => HostLaunchService.SingleFor(items, HostApp.Revit, item => item.Supports(HostApp.Revit)));
    }

    [Fact]
    public void AddLaunch_helpers_register_at_most_one_Supports_per_host_per_contract()
    {
        var services = new ServiceCollection();
        services.AddHostLaunchCore();
        services.AddRevitLaunch(readDocumentYear: null);
        services.AddAutocadFamilyLaunch();
        using var provider = services.BuildServiceProvider();

        foreach (var host in Enum.GetValues<HostApp>())
        {
            Assert.InRange(provider.GetServices<IHostPathResolver>().Count(r => r.Supports(host)), 0, 1);
            Assert.InRange(provider.GetServices<IHostArgumentBuilder>().Count(b => b.Supports(host)), 0, 1);
            Assert.InRange(provider.GetServices<IHostStartupDialogSpec>().Count(s => s.Supports(host)), 0, 1);
        }
    }

    [Fact]
    public void HostLaunchService_throws_when_path_or_args_are_missing()
    {
        var service = new HostLaunchService([], [], []);
        var request = new HostLaunchRequest(HostApp.Navisworks, "2026", null, null);
        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Start(request, TestContext.Current.CancellationToken));
        Assert.Contains("not yet supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostLaunchService_throws_when_argument_builder_returns_empty_argv()
    {
        var service = new HostLaunchService(
            [new StubPathResolver()],
            [new EmptyArgumentBuilder()],
            []);
        var request = new HostLaunchRequest(HostApp.Revit, "2025", null, null);
        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Start(request, TestContext.Current.CancellationToken));
        Assert.Contains("not yet supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HostLaunchService_source_has_no_host_switch()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot.Find(), "source", "DevTools.Hosting", "HostLaunchService.cs"));
        Assert.DoesNotContain("switch (HostApp", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IsAcadFamily", source, StringComparison.Ordinal);
    }

    private sealed class FakeSupport(HostApp host)
    {
        public bool Supports(HostApp value) => value == host;
    }

    private sealed class StubPathResolver : IHostPathResolver
    {
        public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;
        public string? FindExecutable(HostApp hostApp, string version) => @"C:\Windows\System32\cmd.exe";
        public IReadOnlyList<string> GetInstalledVersions(HostApp hostApp) => ["2025"];
    }

    private sealed class EmptyArgumentBuilder : IHostArgumentBuilder
    {
        public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;
        public IReadOnlyList<string> Build(HostLaunchRequest request, string executablePath) => [];
    }
}
