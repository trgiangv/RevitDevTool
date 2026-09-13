namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class TelemetryDsnResolverTests
{
    [TestMethod]
    public void TryResolve_prefers_environment_over_built_in()
    {
        var prev = Environment.GetEnvironmentVariable("SENTRY_DSN");
        try
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", "https://env.example/1");
            Assert.AreEqual("https://env.example/1", TelemetryDsnResolver.TryResolve("https://built.example/2"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", prev);
        }
    }

    [TestMethod]
    public void TryResolve_uses_built_in_when_env_unset()
    {
        var prev = Environment.GetEnvironmentVariable("SENTRY_DSN");
        try
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", null);
            Assert.AreEqual("https://built.example/2", TelemetryDsnResolver.TryResolve("https://built.example/2"));
            Assert.IsNull(TelemetryDsnResolver.TryResolve(null));
            Assert.IsNull(TelemetryDsnResolver.TryResolve("   "));
        }
        finally
        {
            Environment.SetEnvironmentVariable("SENTRY_DSN", prev);
        }
    }
}
