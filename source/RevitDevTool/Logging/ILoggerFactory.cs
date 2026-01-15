using Microsoft.Extensions.Logging;
using RevitDevTool.Settings.Config;

namespace RevitDevTool.Logging;

/// <summary>
/// Factory for creating logger adapters with specific configurations.
/// Allows runtime configuration of logging behavior.
/// </summary>
public interface ILoggerFactory
{
    ILoggerAdapter CreateLogger(LogConfig config, ILogOutputSink? outputSink, bool isDarkTheme);
    void SetMinimumLevel(LogLevel level);
}

