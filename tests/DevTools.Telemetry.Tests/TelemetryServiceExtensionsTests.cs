using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddSingleton<IHostAppInfo>(new FakeHostAppInfo());
        builder.AddTelemetryService(isEnabled);

        using var host = builder.Build();
        return host.Services.GetRequiredService<ITelemetry>();
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => 1;
    }
}
