using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using RevitDevTool.Theme.Design;
// ReSharper disable ReplaceWithFieldKeyword

namespace RevitDevTool.Theme;

/// <summary>
/// Theme resources that support Light/Dark theme switching.
/// This is a static singleton ResourceDictionary - when theme changes,
/// all Views that merged this will automatically update.
/// </summary>
public class ThemeResources : ResourceDictionary, ISupportInitialize
{
    #region Fields

    private static ResourceDictionary? _lightResources;
    private static ResourceDictionary? _darkResources;

    #endregion

    #region Constructor

    public ThemeResources()
    {
        if (Current != null)
        {
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

    private bool IsInitializePending { get; set; }

    private new void BeginInit()
    {
        base.BeginInit();
        IsInitializePending = true;
    }

    private new void EndInit()
    {
        IsInitializePending = false;
        if (DesignMode.IsDesignModeEnabled)
        {
            DesignTimeInit();
        }
        else if (this == Current)
        {
            // Apply default Light theme to ensure resources are loaded
            ApplyApplicationTheme(AppTheme.Light);
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
                EnsureLightResources();
                MergedDictionaries.InsertOrReplace(targetIndex, _lightResources!);
                MergedDictionaries.RemoveIfNotNull(_darkResources);
                break;
        }
    }

    #endregion

    #region Theme Dictionary Initialization

    private static void EnsureLightResources()
    {
        _lightResources ??= ResourceHelper.GetMahAppsLightTheme();
    }

    private static void EnsureDarkResources()
    {
        _darkResources ??= ResourceHelper.GetMahAppsDarkTheme();
    }

    #endregion
}
