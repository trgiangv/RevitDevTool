using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using DevTools.UI.Theme.Design;

namespace DevTools.UI.Theme;

/// <summary>
/// Fluent theme resources that support Light/Dark switching.
/// Merge via <c>FluentTheme.xaml</c> on a window or control — never on
/// <see cref="Application.Current"/>. Same singleton pattern as <see cref="ThemeResources"/>.
/// </summary>
public class FluentThemeResources : ResourceDictionary, ISupportInitialize
{
    private static ResourceDictionary? _lightResources;
    private static ResourceDictionary? _darkResources;

    public FluentThemeResources()
    {
        if (Current != null)
        {
            MergedDictionaries.Add(Current);
            return;
        }

        Current = this;
    }

    public static FluentThemeResources? Current { get; private set; }

    public AppTheme? RequestedTheme
    {
        get => ThemeManager.Current.ApplicationTheme;
        set
        {
            if (ThemeManager.Current.ApplicationTheme == value) return;
            ThemeManager.Current.SetCurrentValue(ThemeManager.ApplicationThemeProperty, value);
            if (DesignMode.IsDesignModeEnabled)
                UpdateDesignTimeThemeDictionary();
        }
    }

    private void DesignTimeInit()
    {
        Debug.Assert(DesignMode.IsDesignModeEnabled);
        UpdateDesignTimeThemeDictionary();
    }

    private void UpdateDesignTimeThemeDictionary()
    {
        Debug.Assert(DesignMode.IsDesignModeEnabled);
        if (IsInitializePending)
            return;

        var appTheme = RequestedTheme ?? AppTheme.Light;
        switch (appTheme)
        {
            case AppTheme.Dark:
                EnsureDarkResources();
                UpdateTo(_darkResources!);
                break;
            case AppTheme.Light:
            case AppTheme.Auto:
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
            MergedDictionaries.RemoveAll<IntellisenseResourcesBase>();
            ApplyApplicationTheme(ThemeManager.Current.ActualApplicationTheme);
        }

        base.EndInit();
    }

    void ISupportInitialize.BeginInit() => BeginInit();

    void ISupportInitialize.EndInit() => EndInit();

    internal void ApplyApplicationTheme(AppTheme theme)
    {
        var targetIndex = DesignMode.IsDesignModeEnabled ? 1 : 0;
        switch (theme)
        {
            case AppTheme.Dark:
                EnsureDarkResources();
                MergedDictionaries.InsertOrReplace(targetIndex, _darkResources!);
                MergedDictionaries.RemoveIfNotNull(_lightResources);
                break;
            case AppTheme.Light:
            default:
                EnsureLightResources();
                MergedDictionaries.InsertOrReplace(targetIndex, _lightResources!);
                MergedDictionaries.RemoveIfNotNull(_darkResources);
                break;
        }
    }

    private static void EnsureLightResources()
    {
        _lightResources ??= ResourceUtils.GetFluentLightTheme();
    }

    private static void EnsureDarkResources()
    {
        _darkResources ??= ResourceUtils.GetFluentDarkTheme();
    }
}
