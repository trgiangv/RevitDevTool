using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging;

public sealed class LoggingConfiguration
{
    private const string LevelKey = "Logging:LogLevel:Default";
    private readonly MemoryConfigurationProvider _provider;

    public LoggingConfiguration(LogLevel initialLevel = LogLevel.Debug)
    {
        var source = new MemoryConfigurationSource
        {
            InitialData = new Dictionary<string, string?>
            {
                [LevelKey] = initialLevel.ToString()
            }
        };

        var builder = new ConfigurationBuilder();
        builder.Add(source);
        Configuration = builder.Build();

        _provider = (MemoryConfigurationProvider)Configuration.Providers.First();
    }

    public IConfigurationRoot Configuration { get; }

    public void SetMinimumLevel(LogLevel level)
    {
        _provider.Set(LevelKey, level.ToString());
        Configuration.Reload();
    }
}
