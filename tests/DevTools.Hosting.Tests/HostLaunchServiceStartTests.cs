using System.Diagnostics;
using DevTools.Hosting;

namespace DevTools.Hosting.Tests;

public sealed class HostLaunchServiceStartTests
{
    [Fact]
    public void SingleFor_returns_default_when_no_match()
    {
        Assert.Null(HostLaunchService.SingleFor(Array.Empty<StubPathResolver>(), HostApp.Revit, static r => r.Supports(HostApp.Revit)));
    }

    [Fact]
    public void Start_throws_when_file_path_missing()
    {
        var service = new HostLaunchService([new StubPathResolver()], [new StubArgumentBuilder()], []);
        var request = new HostLaunchRequest(HostApp.Revit, "2025", @"C:\missing\file.rvt", null);
        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Start(request, TestContext.Current.CancellationToken));
        Assert.Contains("File not found", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_launches_process_with_resolved_version()
    {
        var service = new HostLaunchService([new StubPathResolver()], [new StubArgumentBuilder()], []);
        var request = new HostLaunchRequest(HostApp.Revit, "", null, null);
        var started = service.Start(request, TestContext.Current.CancellationToken);

        try
        {
            Assert.Equal("2025", started.Version);
            Assert.Equal(@"C:\Windows\System32\cmd.exe", started.ExePath, StringComparer.OrdinalIgnoreCase);
            Assert.Equal(HostLaunchRequest.DefaultLanguageCulture, started.LanguageCulture);
            Assert.NotEmpty(started.Arguments);
            Assert.True(started.Process.WaitForExit(5000));
            Assert.True(started.Process.HasExited);
        }
        finally
        {
            started.Process.Dispose();
        }
    }

    [Fact]
    public void Start_throws_when_no_compatible_version()
    {
        var service = new HostLaunchService(
            [new StubPathResolver { InstalledVersions = [] }],
            [new StubArgumentBuilder()],
            []);
        var request = new HostLaunchRequest(HostApp.Revit, "", null, null);
        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Start(request, TestContext.Current.CancellationToken));
        Assert.Contains("No compatible", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_throws_when_executable_not_found()
    {
        var service = new HostLaunchService(
            [new StubPathResolver { ReturnNullExecutable = true }],
            [new StubArgumentBuilder()],
            []);
        var request = new HostLaunchRequest(HostApp.Revit, "2025", null, null);
        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Start(request, TestContext.Current.CancellationToken));
        Assert.Contains("installation not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_wraps_process_start_failures()
    {
        var service = new HostLaunchService(
            [new StubPathResolver { ExecutablePath = @"C:\missing\host.exe" }],
            [new StubArgumentBuilder()],
            []);
        var request = new HostLaunchRequest(HostApp.Revit, "2025", null, null);
        var ex = Assert.Throws<InvalidOperationException>(
            () => service.Start(request, TestContext.Current.CancellationToken));
        Assert.Contains("Failed to launch", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TerminateIfIncomplete_kills_on_timed_out()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c ping -t 127.0.0.1",
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        HostLaunchWaiter.TerminateIfIncomplete(process, HostStatus.TimedOut);
        Assert.True(process.WaitForExit(5000));
    }

    private sealed class StubPathResolver : IHostPathResolver
    {
        public string? ExecutablePath { get; init; } = @"C:\Windows\System32\cmd.exe";
        public bool ReturnNullExecutable { get; init; }
        public IReadOnlyList<string> InstalledVersions { get; init; } = ["2025"];

        public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;

        public string? FindExecutable(HostApp hostApp, string version) =>
            ReturnNullExecutable ? null : ExecutablePath;

        public IReadOnlyList<string> GetInstalledVersions(HostApp hostApp) => InstalledVersions;
    }

    private sealed class StubArgumentBuilder : IHostArgumentBuilder
    {
        public bool Supports(HostApp hostApp) => hostApp == HostApp.Revit;

        public IReadOnlyList<string> Build(HostLaunchRequest request, string executablePath) =>
            ["/c", "exit", "0"];
    }
}
