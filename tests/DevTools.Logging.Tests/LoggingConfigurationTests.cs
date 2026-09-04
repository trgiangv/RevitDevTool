using DevTools.Logging;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void SetMinimumLevel_updates_configuration_section()
    {
        var config = new LoggingConfiguration(LogLevel.Information);
        Assert.Equal("Information", config.LoggingSection["LogLevel:Default"]);

        config.SetMinimumLevel(LogLevel.Warning);
        Assert.Equal("Warning", config.LoggingSection["LogLevel:Default"]);
    }
}
