using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

[TestClass]
public sealed class LoggingExtensionsTests
{
    [TestMethod]
    public void SuppressHostingFrameworkLogs_filters_hosting_categories()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.SuppressHostingFrameworkLogs();
        });
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<ILoggerFactory>();

        Assert.IsFalse(factory.CreateLogger("Microsoft.Extensions.Hosting.Internal.Host").IsEnabled(LogLevel.Information));
        Assert.IsFalse(factory.CreateLogger("Microsoft.Hosting.Lifetime").IsEnabled(LogLevel.Information));
    }

    [TestMethod]
    public void AddLoggingProvider_registers_logging_configuration()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddLoggingProvider();

        using var host = builder.Build();
        Assert.IsNotNull(host.Services.GetService<LoggingConfiguration>());
    }
}
