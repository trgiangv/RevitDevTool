using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

public sealed class LoggingExtensionsTests
{
    [Fact]
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

        Assert.False(factory.CreateLogger("Microsoft.Extensions.Hosting.Internal.Host").IsEnabled(LogLevel.Information));
        Assert.False(factory.CreateLogger("Microsoft.Hosting.Lifetime").IsEnabled(LogLevel.Information));
    }

    [Fact]
    public void AddLoggingProvider_registers_logging_configuration()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.AddLoggingProvider();

        using var host = builder.Build();
        Assert.NotNull(host.Services.GetService<LoggingConfiguration>());
    }
}
