using DevTools.Hosting;

namespace DevTools.Telemetry.Tests;

public sealed class TelemetryServiceRegistrationTests
{
    private const string DummyDsn = "https://publickey@127.0.0.1/1";

    [Fact]
    public void Resolve_returns_NoOp_when_disabled()
    {
        var services = new FakeServices();
        var telemetry = TelemetryServiceRegistration.Resolve(services, _ => false, _ => DummyDsn);
        Assert.IsType<NoOpTelemetry>(telemetry);
    }

    [Fact]
    public void Resolve_returns_NoOp_when_dsn_missing()
    {
        var services = new FakeServices().Add<IHostAppInfo>(new FakeHostAppInfo());
        var telemetry = TelemetryServiceRegistration.Resolve(services, _ => true, _ => null);
        Assert.IsType<NoOpTelemetry>(telemetry);
    }

    [Fact]
    public void Resolve_returns_NoOp_when_enable_callback_throws()
    {
        var services = new FakeServices();
        var telemetry = TelemetryServiceRegistration.Resolve(
            services,
            _ => throw new InvalidOperationException("settings not ready"),
            _ => DummyDsn);
        Assert.IsType<NoOpTelemetry>(telemetry);
    }

    [Fact]
    public void Resolve_returns_Sentry_when_enabled()
    {
        var prev = Environment.GetEnvironmentVariable("SENTRY_DSN");
        try
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", null);
            var services = new FakeServices().Add<IHostAppInfo>(new FakeHostAppInfo());
            using var telemetry = TelemetryServiceRegistration.Resolve(services, _ => true, _ => DummyDsn);
            Assert.IsType<SentryTelemetryService>(telemetry);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", prev);
        }
    }

    private sealed class FakeServices : IServiceProvider
    {
        private readonly Dictionary<Type, object> _map = new();

        public FakeServices Add<T>(T instance) where T : class
        {
            _map[typeof(T)] = instance;
            return this;
        }

        public object? GetService(Type serviceType) =>
            _map.TryGetValue(serviceType, out var value) ? value : null;
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => 1;
    }
}
