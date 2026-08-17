using DevTools.Hosting;
using DevTools.NUnit.Runner;
using DevTools.NUnit.Runner.Commands;
using DevTools.TestRunner.Core.Composition;
using DevTools.TestRunner.Core.Debugging;
using DevTools.TestRunner.Core.Services;

namespace DevTools.NUnit.Runner.Tests;

public sealed class NUnitRunnerModuleTests
{
    [Fact]
    public void Explicit_provider_registration_selects_nunit_module()
    {
        var modules = new RunnerModuleRegistry();
        modules.Register(new NUnitRunnerModule());

        Assert.Equal(["nunit"], modules.RegisteredFrameworkIds);
    }

    [Fact]
    public async Task Discover_is_local_and_never_uses_host_session()
    {
        var hosts = new ThrowingHostSession();
        var modules = new RunnerModuleRegistry();
        modules.Register(new NUnitRunnerModule(), isDefault: true);
        var commands = new NUnitRunnerCommands(
            modules,
            new HostExecutionCoordinator(hosts),
            new ThrowingDebugger());

        var exitCode = await commands.Discover(
            typeof(NUnitRunnerModuleTests).Assembly.Location,
            "Revit",
            "2026",
            hostLaunch: true,
            hostTimeout: 60,
            hostLaunchTimeout: 180,
            framework: "nunit",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, exitCode);
        Assert.Equal(0, hosts.Calls);
    }

    private sealed class ThrowingHostSession : IHostSession
    {
        public int Calls { get; private set; }

        public Task<HostPipeInstance> EnsurePipeAsync(HostApp hostApp, string version, bool forceLaunch, TimeSpan launchTimeout, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new InvalidOperationException("Discovery must not activate a host.");
        }
    }

    private sealed class ThrowingDebugger : IVisualStudioAttach
    {
        public bool TryAttach(int hostProcessId, int? parentProcessId, TextWriter warnings) =>
            throw new InvalidOperationException("Discovery must not attach a debugger.");

        public void TryDetach(int hostProcessId, TextWriter warnings) =>
            throw new InvalidOperationException("Discovery must not detach a debugger.");
    }
}
