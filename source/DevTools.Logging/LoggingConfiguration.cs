using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging;

public sealed class LoggingConfiguration
{
    private readonly Dictionary<string, string?> _memoryData;

    public LoggingConfiguration(LogLevel initialLevel = LogLevel.Debug)
    {
        _memoryData = new Dictionary<string, string?>
        {
            ["Logging:LogLevel:Default"] = initialLevel.ToString()
        };

        Configuration = new ConfigurationBuilder()
            .Add(new MemoryConfigurationSource { InitialData = _memoryData })
            .Build();
    }

    public IConfigurationRoot Configuration { get; }

    public void SetMinimumLevel(LogLevel level)
    {
        _memoryData["Logging:LogLevel:Default"] = level.ToString();
        Configuration.Reload();
    }
}
