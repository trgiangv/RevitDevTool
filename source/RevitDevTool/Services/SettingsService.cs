using System.IO ;
using System.Text.Json ;
using RevitDevTool.Models.Config ;
using Serilog ;
using Wpf.Ui.Appearance ;

namespace RevitDevTool.Services;

public sealed class SettingsService : ISettingsService
{
    public static readonly SettingsService Instance = new();
    private GeneralConfig? _generalConfig;
    private VisualizationConfig? _visualizationConfig;

    public GeneralConfig GeneralConfig
    {
        get
        {
            _generalConfig ??= new GeneralConfig() ;
            return _generalConfig ;
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
    }

    public void LoadSettings()
    {
        LoadApplicationSettings();
        LoadVisualizationSettings();
    }

    private void SaveApplicationSettings()
    {
        var path = SettingsLocation.GetSettingsPath("GeneralConfig.json" );
        var json = JsonSerializer.Serialize(_generalConfig);
        File.WriteAllText(path, json);
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

    public void ResetGeneralSettings()
    {
        _generalConfig = new GeneralConfig
        {
#if REVIT2024_OR_GREATER
            Theme = ApplicationTheme.Auto,
#else
            Theme = ApplicationTheme.Light,
#endif
            UseHardwareRendering = true
        };
    }

    public void ResetVisualizationSettings()
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