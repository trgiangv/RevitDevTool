using DevTools.Logging.Options;
using RevitDevTool.Logging.Enums;
using RevitDevTool.Settings.Config;
using RevitDevTool.Settings.Options;
using System.Diagnostics;
using System.IO;
using AppUtils = DevTools.Utilities.AppUtils;

namespace RevitDevTool.Settings;

public sealed class SettingsService(IFileConfig<PathOptions> fileConfig) : ISettingsService
{
    private GeneralConfig? _generalConfig;
    private LogConfig? _logConfig;
    private VisualizationConfig? _visualizationConfig;
    private ExecutionConfig? _codeExecuteConfig;
    private McpRegistryConfig? _mcpRegistryConfig;

    public GeneralConfig GeneralConfig
    {
        get
        {
            _generalConfig ??= new GeneralConfig();
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

    public HashSet<RevitEnricher> RevitEnrichers { get; set; } = [RevitEnricher.RevitVersion, RevitEnricher.RevitDocumentTitle];

    public VisualizationConfig VisualizationConfig
    {
        get
        {
            _visualizationConfig ??= new VisualizationConfig();
            return _visualizationConfig;
        }
    }

    public ExecutionConfig ExecutionConfig
    {
        get
        {
            _codeExecuteConfig ??= new ExecutionConfig();
            return _codeExecuteConfig;
        }
    }

    public McpRegistryConfig McpRegistryConfig
    {
        get
        {
            _mcpRegistryConfig ??= new McpRegistryConfig();
            return _mcpRegistryConfig;
        }
    }

    public void SaveSettings()
    {
        SaveApplicationSettings();
        SaveVisualizationSettings();
        SaveLogSettings();
        SaveCodeExecuteSettings();
        SaveMcpRegistrySettings();
    }

    public void LoadSettings()
    {
        LoadApplicationSettings();
        LoadVisualizationSettings();
        LoadLogSettings();
        LoadCodeExecuteSettings();
        LoadMcpRegistrySettings();
    }

    public void ResetSettings()
    {
        ResetGeneralSettings();
        ResetVisualizationSettings();
        ResetLogSettings();
    }

    private void SaveApplicationSettings()
    {
        if (_generalConfig is null) return;
        fileConfig.Save(_generalConfig);
    }

    private void SaveLogSettings()
    {
        if (_logConfig is null) return;
        fileConfig.Save(_logConfig);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.TraceListener.WpfTraceLevel;
    }

    private void SaveVisualizationSettings()
    {
        if (_visualizationConfig is null) return;
        fileConfig.Save(_visualizationConfig);
    }

    private void LoadApplicationSettings()
    {
        try
        {
            _generalConfig = fileConfig.Load<GeneralConfig>();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Application settings loading error: {exception.Message}");
        }

        if (_generalConfig is null)
        {
            ResetGeneralSettings();
        }
    }

    private void LoadVisualizationSettings()
    {
        try
        {
            _visualizationConfig = fileConfig.Load<VisualizationConfig>();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Visualization settings loading error: {exception.Message}");
        }

        if (_visualizationConfig is null)
        {
            ResetVisualizationSettings();
        }
    }

    private void LoadLogSettings()
    {
        try
        {
            _logConfig = fileConfig.Load<LogConfig>();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Log settings loading error: {exception.Message}");
        }

        if (_logConfig is null)
        {
            ResetLogSettings();
        }
        else
        {
            EnsureLogFolder(_logConfig);
            PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.TraceListener.WpfTraceLevel;
        }
    }

    private void ResetGeneralSettings()
    {
        _generalConfig = new GeneralConfig
        {
#if REVIT2024_OR_GREATER
            Theme = Theme.AppTheme.Auto,
#else
            Theme = Theme.AppTheme.Light,
#endif
            UseHardwareRendering = true,
            IsTraceEnabled = true,
        };
    }

    private void ResetLogSettings()
    {
        _logConfig = new LogConfig();
        EnsureLogFolder(_logConfig);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.TraceListener.WpfTraceLevel;
    }

    private void ResetVisualizationSettings()
    {
        _visualizationConfig = new VisualizationConfig
        {
            BoundingBoxSettings = new BoundingBoxVisualizationSettings(),
            FaceSettings = new FaceVisualizationSettings(),
            MeshSettings = new MeshVisualizationSettings(),
            PolylineSettings = new PolylineVisualizationSettings(),
            SolidSettings = new SolidVisualizationSettings(),
            XyzSettings = new XyzVisualizationSettings()
        };
    }

    private void EnsureLogFolder(LogConfig config)
    {
        if (AppUtils.IsValidPath(config.FileLogging.LogFolder)) return;
        config.FileLogging.LogFolder = fileConfig.Options.LogsDirectory;
    }

    private void SaveCodeExecuteSettings()
    {
        if (_codeExecuteConfig is null) return;
        _codeExecuteConfig.DotnetAssemblyPaths.RemoveAll(path => !File.Exists(path));
        _codeExecuteConfig.ScriptFolderPaths.RemoveAll(path => !Directory.Exists(path));
        fileConfig.Save(_codeExecuteConfig);
    }

    private void LoadCodeExecuteSettings()
    {
        try
        {
            _codeExecuteConfig = fileConfig.Load<ExecutionConfig>();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"Code execute settings loading error: {exception.Message}");
        }

        _codeExecuteConfig ??= new ExecutionConfig();
    }

    private void SaveMcpRegistrySettings()
    {
        if (_mcpRegistryConfig is null)
            return;

        _mcpRegistryConfig.DotnetPaths.RemoveAll(path =>
            string.IsNullOrWhiteSpace(path) ||
            !File.Exists(path) ||
            !string.Equals(Path.GetExtension(path), ".dll", StringComparison.OrdinalIgnoreCase));
        _mcpRegistryConfig.PythonToolsetPaths.RemoveAll(path =>
            string.IsNullOrWhiteSpace(path) || !Directory.Exists(path));

        fileConfig.Save(_mcpRegistryConfig);
    }

    private void LoadMcpRegistrySettings()
    {
        try
        {
            _mcpRegistryConfig = fileConfig.Load<McpRegistryConfig>();
        }
        catch (Exception exception)
        {
            Trace.TraceError($"MCP registry settings loading error: {exception.Message}");
        }

        _mcpRegistryConfig ??= new McpRegistryConfig();
    }
}
