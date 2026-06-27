using System.Diagnostics;
using System.IO;
using AcadDevTool.Logging.Enums;
using DevTools.Logging;
using DevTools.Settings;
using DevTools.Settings.Configs;
using DevTools.UI.Theme;
using Microsoft.Extensions.Logging;
using ZLogger;
using AppUtils = DevTools.Utilities.AppUtils;

namespace AcadDevTool.Settings;

public sealed class SettingsService(IFileConfig<PathOptions> fileConfig, ILogger<SettingsService> logger) : IAcadSettingsService
{
    public HashSet<AcadEnricher> AcadEnrichers { get; set; } =
        [AcadEnricher.AcadVersion, AcadEnricher.AcadDocumentTitle];

    private GeneralConfig? _generalConfig;
    private LogConfig? _logConfig;
    private AcadExecutionConfig? _executionConfig;
    private AcadMcpRegistryConfig? _mcpRegistryConfig;

    public GeneralConfig GeneralConfig
    {
        get
        {
            _generalConfig ??= CreateDefaultGeneralConfig();
            return _generalConfig;
        }
    }

    public LogConfig LogConfig
    {
        get
        {
            _logConfig ??= new LogConfig();
            EnsureLogFolder(_logConfig);
            return _logConfig;
        }
    }

    public ExecutionConfig ExecutionConfig
    {
        get
        {
            _executionConfig ??= new AcadExecutionConfig();
            return _executionConfig;
        }
    }

    public McpRegistryConfig McpRegistryConfig
    {
        get
        {
            _mcpRegistryConfig ??= new AcadMcpRegistryConfig();
            return _mcpRegistryConfig;
        }
    }

    public void SaveSettings()
    {
        SaveConfig(_generalConfig);
        SaveLogSettings();
        SaveExecutionSettings();
        SaveMcpRegistrySettings();
    }

    public void LoadSettings()
    {
        LoadApplicationSettings();
        LoadLogSettings();
        LoadExecutionSettings();
        LoadMcpRegistrySettings();
    }

    public void ResetSettings()
    {
        _generalConfig = CreateDefaultGeneralConfig();
        _logConfig = new LogConfig();
        EnsureLogFolder(_logConfig);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.TraceListener.WpfTraceLevel;
        TraceListenerHelper.ApplyPresentationTraceSwitches(_logConfig.TraceListener.WpfTraceLevel);
    }

    private void SaveConfig<T>(T? config) where T : class
    {
        if (config is null) return;
        fileConfig.Save(config);
    }

    private void SaveLogSettings()
    {
        if (_logConfig is null) return;
        fileConfig.Save(_logConfig);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.TraceListener.WpfTraceLevel;
    }

    private void SaveExecutionSettings()
    {
        if (_executionConfig is null) return;
        _executionConfig.DotnetAssemblyPaths.RemoveAll(path => !File.Exists(path));
        _executionConfig.ScriptFolderPaths.RemoveAll(path => !Directory.Exists(path));
        fileConfig.Save(_executionConfig);
    }

    private void SaveMcpRegistrySettings()
    {
        if (_mcpRegistryConfig is null) return;
        _mcpRegistryConfig.DotnetPaths.RemoveAll(path =>
            string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) ||
            !string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase));
        _mcpRegistryConfig.PythonToolsetPaths.RemoveAll(path =>
            string.IsNullOrWhiteSpace(path) || !Directory.Exists(path));
        fileConfig.Save(_mcpRegistryConfig);
    }

    private void LoadApplicationSettings()
    {
        try { _generalConfig = fileConfig.Load<GeneralConfig>(); }
        catch (Exception ex) { logger.ZLogError($"Application settings loading error: {ex.Message}"); }
        _generalConfig ??= CreateDefaultGeneralConfig();
    }

    private static GeneralConfig CreateDefaultGeneralConfig()
    {
        return new GeneralConfig { Theme = AppTheme.Auto };
    }

    private void LoadLogSettings()
    {
        try { _logConfig = fileConfig.Load<LogConfig>(); }
        catch (Exception ex) { logger.ZLogError($"Log settings loading error: {ex.Message}"); }

        if (_logConfig is null)
        {
            _logConfig = new LogConfig();
            EnsureLogFolder(_logConfig);
        }
        else
        {
            EnsureLogFolder(_logConfig);
            PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.TraceListener.WpfTraceLevel;
        }
    }

    private void LoadExecutionSettings()
    {
        try { _executionConfig = fileConfig.Load<AcadExecutionConfig>(); }
        catch (Exception ex) { logger.ZLogError($"Code execute settings loading error: {ex.Message}"); }
        _executionConfig ??= new AcadExecutionConfig();
    }

    private void LoadMcpRegistrySettings()
    {
        try { _mcpRegistryConfig = fileConfig.Load<AcadMcpRegistryConfig>(); }
        catch (Exception ex) { logger.ZLogError($"MCP registry settings loading error: {ex.Message}"); }
        _mcpRegistryConfig ??= new AcadMcpRegistryConfig();
    }

    private void EnsureLogFolder(LogConfig config)
    {
        if (AppUtils.IsValidPath(config.FileLogging.LogFolder)) return;
        config.FileLogging.LogFolder = fileConfig.Options.LogsDirectory;
    }
}
