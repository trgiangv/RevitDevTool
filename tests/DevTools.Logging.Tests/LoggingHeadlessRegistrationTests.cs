using DevTools.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

[TestClass]
public sealed class LoggingHeadlessRegistrationTests
{
    [TestMethod]
    public void AddLoggingProvider_resolves_ILogger_without_Presentation()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });

        builder.AddLoggingProvider();

        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<LoggingHeadlessRegistrationTests>>();
        Assert.IsNotNull(logger);

        var references = typeof(LoggingExtensions).Assembly
            .GetReferencedAssemblies()
            .Select(static reference => reference.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("DevTools.Presentation", references);
        Assert.DoesNotContain("ZLogger.Scintilla", references);
        Assert.DoesNotContain("PresentationFramework", references);
        Assert.DoesNotContain("DevTools.UI", references);
    }
}
