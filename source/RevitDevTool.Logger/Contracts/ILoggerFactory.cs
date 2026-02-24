using Microsoft.Extensions.Logging;
using RevitDevTool.Logger.Config;
namespace RevitDevTool.Logger.Contracts;

/// <summary>
/// Factory for creating logger adapters with specific configurations.
/// Allows runtime configuration of logging behavior.
/// </summary>
public interface ILoggerFactory
{
    ILoggerAdapter CreateLogger(LogConfigCore config, ILogOutputSink? outputSink, bool isDarkTheme);
    void SetMinimumLevel(LogLevel level);
}
