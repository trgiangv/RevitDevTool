using System.Diagnostics;
using System.IO ;
using System.Text.Json ;
using RevitDevTool.Models.Config ;
using Serilog ;
using Wpf.Ui.Appearance ;

namespace RevitDevTool.Services;

public sealed class SettingsService
{
    public event Action? LogSettingsChanged;
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
        var path = SettingsLocation.GetSettingsPath("GeneralConfig.json" );
        var json = JsonSerializer.Serialize(_generalConfig);
        File.WriteAllText(path, json);
    }
    
    private void SaveLogSettings()
    {
        var path = SettingsLocation.GetSettingsPath("LogConfig.json" );
        var json = JsonSerializer.Serialize(_logConfig);
        File.WriteAllText(path, json);
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig?.WpfTraceLevel ?? SourceLevels.Warning;
        LogSettingsChanged?.Invoke();
    }

    private void SaveVisualizationSettings()
    {
        var path = SettingsLocation.GetSettingsPath("VisualizationConfig.json" );
        var json = JsonSerializer.Serialize(_visualizationConfig);
        File.WriteAllText(path, json);
    }

    private void LoadApplicationSettings()
    {
        var path = SettingsLocation.GetSettingsPath("GeneralConfig.json" );
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
        var path = SettingsLocation.GetSettingsPath("VisualizationConfig.json" );
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
        var path = SettingsLocation.GetSettingsPath("LogConfig.json" );
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
            Theme = ApplicationTheme.Auto,
#else
            Theme = ApplicationTheme.Light,
#endif
            UseHardwareRendering = true,
        };
    }

    private void ResetLogSettings()
    {
        _logConfig = new LogConfig();
        PresentationTraceSources.DataBindingSource.Switch.Level = _logConfig.WpfTraceLevel;
        LogSettingsChanged?.Invoke();
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