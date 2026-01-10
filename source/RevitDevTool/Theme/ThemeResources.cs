using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using RevitDevTool.Theme.Design;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.UI;
// ReSharper disable ReplaceWithFieldKeyword
#endif

namespace RevitDevTool.Theme;

/// <summary>
/// Theme resources that support Light/Dark theme switching.
/// This is a static singleton ResourceDictionary - when theme changes,
/// all Views that merged this will automatically update.
/// </summary>
public class ThemeResources : ResourceDictionary, ISupportInitialize
{
    #region Fields

    private ResourceDictionary? _lightResources;
    private ResourceDictionary? _darkResources;
    private bool _usingRevitTheme;

    #endregion

    #region Constructor

    public ThemeResources()
    {
        if (Current != null)
        {
            // Add reference to the singleton instance so all views share the same theme
            MergedDictionaries.Add(Current);
            return;
        }
        Current = this;
    }

    #endregion

    #region Properties

    public static ThemeResources? Current { get; private set; }

    /// <summary>
    /// Gets or sets a value that determines the light-dark preference for the overall theme of an app.
    /// </summary>
    public AppTheme? RequestedTheme
    {
        get => ThemeManager.Current.ApplicationTheme;
        set
        {
            if (ThemeManager.Current.ApplicationTheme == value) return;
            ThemeManager.Current.SetCurrentValue(ThemeManager.ApplicationThemeProperty, value);
            if (DesignMode.IsDesignModeEnabled)
            {
                UpdateDesignTimeThemeDictionary();
            }
        }
    }

    public bool UsingRevitTheme
    {
        get => _usingRevitTheme;
        set
        {
            _usingRevitTheme = value;
            if (ThemeManager.Current.UseRevitTheme != value)
            {
                ThemeManager.Current.UseRevitTheme = value;
            }
        }
    }

    #endregion

    #region Design Time

    private void DesignTimeInit()
    {
        Debug.Assert(DesignMode.IsDesignModeEnabled);
        UpdateDesignTimeThemeDictionary();
    }

    private void UpdateDesignTimeThemeDictionary()
    {
        Debug.Assert(DesignMode.IsDesignModeEnabled);
        if (IsInitializePending)
        {
            return;
        }
        var appTheme = RequestedTheme ?? AppTheme.Light;
        switch (appTheme)
        {
            case AppTheme.Light:
                EnsureLightResources();
                UpdateTo(_lightResources!);
                break;
            case AppTheme.Dark:
                EnsureDarkResources();
                UpdateTo(_darkResources!);
                break;
            default:
                EnsureLightResources();
                UpdateTo(_lightResources!);
                break;
        }
        return;

        void UpdateTo(ResourceDictionary themeDictionary)
        {
            MergedDictionaries.RemoveIfNotNull(_lightResources);
            MergedDictionaries.RemoveIfNotNull(_darkResources);
            MergedDictionaries.Insert(0, themeDictionary);
        }
    }

    #endregion

    #region ISupportInitialize

    private bool IsInitialized { get; set; }
    private bool IsInitializePending { get; set; }

    private new void BeginInit()
    {
        base.BeginInit();
        IsInitializePending = true;
        IsInitialized = false;
    }

    private new void EndInit()
    {
        IsInitializePending = false;
        IsInitialized = true;
        if (DesignMode.IsDesignModeEnabled)
        {
            DesignTimeInit();
        }
        else
        {
            ThemeManager.Current.Initialize();
        }
        base.EndInit();
    }

    void ISupportInitialize.BeginInit()
    {
        BeginInit();
    }

    void ISupportInitialize.EndInit()
    {
        EndInit();
    }

    #endregion

    #region Theme Application

    internal void ApplyApplicationTheme(AppTheme theme)
    {
        var targetIndex = DesignMode.IsDesignModeEnabled ? 1 : 0;
        switch (theme)
        {
            case AppTheme.Light:
                EnsureLightResources();
                MergedDictionaries.InsertOrReplace(targetIndex, _lightResources!);
                MergedDictionaries.RemoveIfNotNull(_darkResources);
                break;
            case AppTheme.Dark:
                EnsureDarkResources();
                MergedDictionaries.InsertOrReplace(targetIndex, _darkResources!);
                MergedDictionaries.RemoveIfNotNull(_lightResources);
                break;
            default:
                // For Auto, default to Light (actual theme determined by ThemeManager)
                EnsureLightResources();
                MergedDictionaries.InsertOrReplace(targetIndex, _lightResources!);
                MergedDictionaries.RemoveIfNotNull(_darkResources);
                break;
        }
    }

    #endregion

    #region Theme Dictionary Initialization

    private void EnsureLightResources()
    {
        _lightResources ??= ResourceHelper.GetMahAppsLightTheme();
    }

    private void EnsureDarkResources()
    {
        _darkResources ??= ResourceHelper.GetMahAppsDarkTheme();
    }

    #endregion
}
