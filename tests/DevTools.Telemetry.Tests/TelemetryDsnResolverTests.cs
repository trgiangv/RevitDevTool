namespace DevTools.Telemetry.Tests;

public sealed class TelemetryDsnResolverTests
{
    [Fact]
    public void TryResolve_prefers_environment_over_built_in()
    {
        var prev = Environment.GetEnvironmentVariable("SENTRY_DSN");
        try
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", "https://env.example/1");
            Assert.Equal("https://env.example/1", TelemetryDsnResolver.TryResolve("https://built.example/2"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", prev);
        }
    }

    [Fact]
    public void TryResolve_uses_built_in_when_env_unset()
    {
        var prev = Environment.GetEnvironmentVariable("SENTRY_DSN");
        try
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", null);
            Assert.Equal("https://built.example/2", TelemetryDsnResolver.TryResolve("https://built.example/2"));
            Assert.Null(TelemetryDsnResolver.TryResolve(null));
            Assert.Null(TelemetryDsnResolver.TryResolve("   "));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", prev);
        }
    }
}
