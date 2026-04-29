using DevTools.Execution.Settings;
using DevTools.Logging.Options;

namespace DevTools.Presentation.Interfaces;

public interface IDevToolsSettingsService : ISettingsService
{
    LogConfig LogConfig { get; }
    void ResetSettings();
}
