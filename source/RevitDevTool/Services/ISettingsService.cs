using RevitDevTool.Models.Config ;

namespace RevitDevTool.Services ;

/// <summary>
///     Service for managing the application settings.
/// </summary>
public interface ISettingsService
{
  /// <summary>
  ///     Represents the application settings.
  /// </summary>
  public GeneralConfig GeneralConfig { get; }
    
  /// <summary>
  ///     Represents the visualization settings.
  /// </summary>
  public VisualizationConfig VisualizationConfig { get; }
    
  /// <summary>
  ///     Save the settings to the storage.
  /// </summary>
  void SaveSettings();
    
  /// <summary>
  ///     Load the settings from the storage.
  /// </summary>
  void LoadSettings();
    
  /// <summary>
  ///     Reset the application settings to the default values. Only in-memory settings will be affected.
  /// </summary>
  void ResetGeneralSettings();
  
  /// <summary>
  ///     Reset the visualization settings to the default values. Only in-memory settings will be affected.
  /// </summary>
  void ResetVisualizationSettings();
}