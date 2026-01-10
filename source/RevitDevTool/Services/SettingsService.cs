using System.Diagnostics;
using System.IO;
using System.Text.Json;
using RevitDevTool.Models.Config;
using RevitDevTool.Theme;
using RevitDevTool.Utils;
using Serilog;

namespace RevitDevTool.Services;

public sealed class SettingsService : ISettingsService
{
    public static readonly SettingsService Instance = new();
    private GeneralConfig? _generalConfig;
    private LogConfig? _logConfig;
    private VisualizationConfig? _visualizationConfig;

    public GeneralConfig GeneralConfig
    {
        get
        {
            _generalConfig ??= new GeneralConfig() ;
            return _generalConfig ;
        }
    }
    
    public LogConfig LogConfig
    {
        get
        {
            _logConfig ??= new LogConfig() ;
            return _logConfig ;
        }
    }
    
    public VisualizationConfig VisualizationConfig
    {
        get
        {
            _visualizationConfig ??= new VisualizationConfig() ;
            return _visualizationConfig ;
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
        var path = SettingsUtils.GetSettingsPath("GeneralConfig.json" );
        var json = JsonSerializer.Serialize(_generalConfig);
        File.WriteAllText(path, json);
    }
    
    private void SaveLogSettings()
    {
        var path = SettingsUtils.GetSettingsPath("LogConfig.json" );
        var json = JsonSerializer.Serialize(_logConfig);
        File.WriteAllText(path, json);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig?.WpfTraceLevel ?? SourceLevels.Warning;
    }

    private void SaveVisualizationSettings()
    {
        var path = SettingsUtils.GetSettingsPath("VisualizationConfig.json" );
        var json = JsonSerializer.Serialize(_visualizationConfig);
        File.WriteAllText(path, json);
    }

    private void LoadApplicationSettings()
    {
        var path = SettingsUtils.GetSettingsPath("GeneralConfig.json" );
        if (!File.Exists(path))
        {
            ResetGeneralSettings();
            return;
        }

        try
        {
            using var config = File.OpenRead(path);
            _generalConfig = JsonSerializer.Deserialize<GeneralConfig>(config);
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
        var path = SettingsUtils.GetSettingsPath("VisualizationConfig.json" );
        if (!File.Exists(path))
        {
            ResetVisualizationSettings();
            return;
        }

        try
        {
            using var config = File.OpenRead(path);
            _visualizationConfig = JsonSerializer.Deserialize<VisualizationConfig>(config);
        }
        catch (Exception exception)
        {
            Log.Logger.Error(exception, "Application settings loading error");
        }

        if (_visualizationConfig is null)
        {
            ResetVisualizationSettings();
        }
    }
    
    private void LoadLogSettings()
    {
        var path = SettingsUtils.GetSettingsPath("LogConfig.json" );
        if (!File.Exists(path))
        {
            ResetLogSettings();
            return;
        }

        try
        {
            using var config = File.OpenRead(path);
            _logConfig = JsonSerializer.Deserialize<LogConfig>(config);
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
}