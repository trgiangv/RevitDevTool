using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class TelemetryServiceExtensionsTests
{
    [TestMethod]
    public void AddTelemetryService_returns_NoOp_when_disabled()
    {
        Assert.IsInstanceOfType<NoOpTelemetry>(Resolve(_ => false));
    }

    [TestMethod]
    public void AddTelemetryService_returns_NoOp_when_enable_callback_throws()
    {
        Assert.IsInstanceOfType<NoOpTelemetry>(Resolve(_ => throw new InvalidOperationException("settings not ready")));
    }

    private static ITelemetry Resolve(Func<IServiceProvider, bool> isEnabled)
    {
        using var provider = new ServiceCollection()
            .AddSingleton<IHostAppInfo>(new FakeHostAppInfo())
            .AddTelemetryService(isEnabled)
            .BuildServiceProvider();
        return provider.GetRequiredService<ITelemetry>();
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => 1;
    }
}
