using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using DevTools.UI.Theme.Design;
// ReSharper disable ReplaceWithFieldKeyword

namespace DevTools.UI.Theme;

/// <summary>
/// Theme resources that support Light/Dark theme switching.
/// This is a static singleton ResourceDictionary - when theme changes,
/// all Views that merged this will automatically update.
/// </summary>
public class ThemeResources : ResourceDictionary, ISupportInitialize
{
    #region Fields

    private bool _canBeAccessedAcrossThreads;
    private static ResourceDictionary? lightResources;
    private static ResourceDictionary? darkResources;

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

    public bool CanBeAccessedAcrossThreads
    {
        get => _canBeAccessedAcrossThreads;
        set
        {
            if (DesignMode.IsDesignModeEnabled) return;
            if (IsInitialized)
            {
                throw new InvalidOperationException();
            }
            _canBeAccessedAcrossThreads = value;
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
                UpdateTo(lightResources!);
                break;
            case AppTheme.Dark:
                EnsureDarkResources();
                UpdateTo(darkResources!);
                break;
            case AppTheme.Auto:
            default:
                EnsureLightResources();
                UpdateTo(lightResources!);
                break;
        }
        return;

        void UpdateTo(ResourceDictionary themeDictionary)
        {
            MergedDictionaries.RemoveIfNotNull(lightResources);
            MergedDictionaries.RemoveIfNotNull(darkResources);
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
        else if (this == Current)
        {
            // Remove any IntellisenseResources that were loaded at design time
            // to avoid duplicate resources at runtime
            MergedDictionaries.RemoveAll<IntellisenseResourcesBase>();

            ApplyApplicationTheme(ThemeManager.Current.ActualApplicationTheme);

            if (CanBeAccessedAcrossThreads)
            {
                // Preload and seal both theme dictionaries for thread-safe access
                EnsureLightResources();
                EnsureDarkResources();
                lightResources?.SealValues();
                darkResources?.SealValues();
            }
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
                MergedDictionaries.InsertOrReplace(targetIndex, lightResources!);
                MergedDictionaries.RemoveIfNotNull(darkResources);
                break;
            case AppTheme.Dark:
                EnsureDarkResources();
                MergedDictionaries.InsertOrReplace(targetIndex, darkResources!);
                MergedDictionaries.RemoveIfNotNull(lightResources);
                break;
            default:
                EnsureLightResources();
                MergedDictionaries.InsertOrReplace(targetIndex, lightResources!);
                MergedDictionaries.RemoveIfNotNull(darkResources);
                break;
        }
    }

    #endregion

    #region Theme Dictionary Initialization

    private static void EnsureLightResources()
    {
        lightResources ??= ResourceUtils.GetHandyLightTheme();
    }

    private static void EnsureDarkResources()
    {
        darkResources ??= ResourceUtils.GetHandyDarkTheme();
    }

    #endregion
}
