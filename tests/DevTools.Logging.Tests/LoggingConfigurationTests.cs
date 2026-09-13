using DevTools.Logging;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging.Tests;

[TestClass]
public sealed class LoggingConfigurationTests
{
    [TestMethod]
    public void SetMinimumLevel_updates_configuration_section()
    {
        var config = new LoggingConfiguration(LogLevel.Information);
        Assert.AreEqual("Information", config.LoggingSection["LogLevel:Default"]);

        config.SetMinimumLevel(LogLevel.Warning);
        Assert.AreEqual("Warning", config.LoggingSection["LogLevel:Default"]);
    }
}
