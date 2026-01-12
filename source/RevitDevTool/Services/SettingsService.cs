using RevitDevTool.Models.Config;
using RevitDevTool.Services.Configuration;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using Serilog;
using System.Diagnostics;

namespace RevitDevTool.Services;

public sealed class SettingsService(IFileConfig<PathOptions> fileConfig) : ISettingsService
{
    private GeneralConfig? _generalConfig;
    private LogConfig? _logConfig;
    private VisualizationConfig? _visualizationConfig;

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

    public VisualizationConfig VisualizationConfig
    {
        get
        {
            _visualizationConfig ??= new VisualizationConfig();
            return _visualizationConfig;
        }
    }

    public void SaveSettings()
    {
        SaveApplicationSettings();
        SaveVisualizationSettings();
        SaveLogSettings();
    }

    public void LoadSettings()
    {
        LoadApplicationSettings();
        LoadVisualizationSettings();
        LoadLogSettings();
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
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.WpfTraceLevel;
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
            Log.Logger.Error(exception, "Application settings loading error");
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
            Log.Logger.Error(exception, "Visualization settings loading error");
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
            Log.Logger.Error(exception, "Log settings loading error");
        }

        if (_logConfig is null)
        {
            ResetLogSettings();
        }
        else
        {
            EnsureLogFolder(_logConfig);
            PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.WpfTraceLevel;
        }
    }

    private void ResetGeneralSettings()
    {
        _generalConfig = new GeneralConfig
        {
#if REVIT2024_OR_GREATER
            Theme = AppTheme.Auto,
#else
            Theme = AppTheme.Light,
#endif
            UseHardwareRendering = true,
        };
    }

    private void ResetLogSettings()
    {
        _logConfig = new LogConfig();
        EnsureLogFolder(_logConfig);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.WpfTraceLevel;
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
        if (SettingsUtils.IsValidPath(config.LogFolder)) return;
        config.LogFolder = fileConfig.Options.LogsDirectory;
    }
}