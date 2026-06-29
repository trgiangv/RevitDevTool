using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.Logging;

namespace DevTools.Logging;

public sealed class LoggingConfiguration
{
    private const string LevelKey = "Logging:LogLevel:Default";
    private const string LoggingSectionKey = "Logging";
    private readonly MemoryConfigurationProvider _provider;
    private readonly IConfigurationRoot _configuration;
    
    public IConfigurationSection LoggingSection { get; }

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
        _configuration = builder.Build();
        LoggingSection = _configuration.GetSection(LoggingSectionKey);

        _provider = (MemoryConfigurationProvider)_configuration.Providers.First();
    }

    public void SetMinimumLevel(LogLevel level)
    {
        _provider.Set(LevelKey, level.ToString());
        _configuration.Reload();
    }
}
