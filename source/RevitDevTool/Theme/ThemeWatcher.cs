using System.Windows;
using RevitDevTool.Services;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Color = System.Windows.Media.Color;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
#endif

namespace RevitDevTool.Theme;

public sealed class ThemeWatcherService(ISettingsService settingsService) : IThemeWatcherService
{
#if REVIT2024_OR_GREATER
    private bool _isWatchingRevitTheme;
#endif

    private readonly List<FrameworkElement> _observedElements = [];
    private readonly HashSet<FrameworkElement> _uniqueObservedElements = [];

    public void Initialize()
    {
        UiApplication.Current.Resources = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/RevitDevTool;component/Theme/Theme.xaml", UriKind.Absolute)
        };

        ApplicationThemeManager.Changed += OnApplicationThemeManagerChanged;
    }

    public void Watch(FrameworkElement frameworkElement)
    {
        ApplicationThemeManager.Apply(frameworkElement);
        frameworkElement.Loaded += OnWatchedElementLoaded;
        frameworkElement.Unloaded += OnWatchedElementUnloaded;
    }

    public void Unwatch()
    {
#if REVIT2024_OR_GREATER
        if (!_isWatchingRevitTheme) return;

        ExternalEventController.ActionEventHandler.Raise(application =>
            application.ThemeChanged -= OnRevitThemeChanged);
        _isWatchingRevitTheme = false;
#endif
    }

#if REVIT2024_OR_GREATER
    private static ApplicationTheme GetRevitTheme()
    {
        return UIThemeManager.CurrentTheme switch
        {
            UITheme.Light => ApplicationTheme.Light,
            UITheme.Dark => ApplicationTheme.Dark,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private void OnRevitThemeChanged(object? sender, ThemeChangedEventArgs args)
    {
        if (args.ThemeChangedType != ThemeType.UITheme) return;

        if (_observedElements.Count > 0)
        {
            _observedElements[0].Dispatcher.Invoke(ApplyTheme);
        }
    }
#endif
    
    private void OnApplicationThemeManagerChanged(ApplicationTheme applicationTheme, Color accent)
    {
        foreach (var frameworkElement in _observedElements)
        {
            ApplicationThemeManager.Apply(frameworkElement);
            UpdateDictionary(frameworkElement);
        }
    }

    private void OnWatchedElementLoaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement) sender;
        _observedElements.Add(element);

        var requiredTheme = GetEffectiveTheme();

        // Old (known-good) behavior: avoid re-applying theme multiple times for same element,
        // but do force-apply when the element's current theme differs from required.
        if (GetFrameworkElementTheme(element) == requiredTheme && !_uniqueObservedElements.Add(element)) return;

        // Ensure the element uses the global (current) Wpf.Ui theme + control dictionaries.
        // This is critical for add-in scenarios where views may declare their own ThemesDictionary Theme="Light",
        // and for dockable pane content which may be created early but loaded much later.
        UpdateDictionary(element);

        // Copy current global resources (including accent brushes) into the element.
        ApplicationThemeManager.Apply(element);

        // Re-apply required theme globally (also updates accent resources).
        ApplicationThemeManager.Apply(requiredTheme);
    }

    private void OnWatchedElementUnloaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement) sender;
        _observedElements.Remove(element);
        if (element is Window)
            _uniqueObservedElements.Clear();
    }

    public void ApplyTheme()
    {
        var theme = settingsService.GeneralConfig.Theme;

#if REVIT2024_OR_GREATER
        if (theme == ApplicationTheme.Auto)
        {
            theme = GetRevitTheme();

            if (!_isWatchingRevitTheme)
            {
                ExternalEventController.ActionEventHandler.Raise(application => application.ThemeChanged += OnRevitThemeChanged);
                _isWatchingRevitTheme = true;
            }
        }
        else if (_isWatchingRevitTheme)
        {
            ExternalEventController.ActionEventHandler.Raise(application => application.ThemeChanged -= OnRevitThemeChanged);
            _isWatchingRevitTheme = false;
        }
#endif

        ApplicationThemeManager.Apply(theme);
        UpdateBackground(theme);
    }

    private static void UpdateDictionary(FrameworkElement frameworkElement)
    {
        if (UiApplication.Current.Resources.MergedDictionaries.Count < 2)
            return;

        var merged = frameworkElement.Resources.MergedDictionaries;

        var globalTheme = UiApplication.Current.Resources.MergedDictionaries[0];
        var globalControls = UiApplication.Current.Resources.MergedDictionaries[1];

        // Remove any existing occurrences of the global dictionaries (avoid duplicates).
        while (merged.Contains(globalTheme)) merged.Remove(globalTheme);
        while (merged.Contains(globalControls)) merged.Remove(globalControls);

        // Remove any locally declared themed dictionaries (RevitDevTool.UI / LookupEngine.UI theme + controls).
        // These often get declared as <ui:ThemesDictionary Theme="Light" />, which would freeze the theme/accent.
        for (var i = merged.Count - 1; i >= 0; i--)
        {
            var dictionary = merged[i];
            if (dictionary.Source is null) continue;

            var source = dictionary.Source.OriginalString;
            // var isWpfUiFork =
            //     source.Contains("RevitDevTool.UI;", StringComparison.OrdinalIgnoreCase)
            //     || source.Contains("LookupEngine.UI;", StringComparison.OrdinalIgnoreCase);
            //
            // if (!isWpfUiFork) continue;

            var isThemeOrControls =
                source.Contains("/Resources/Theme/", StringComparison.OrdinalIgnoreCase)
                || source.Contains("/Resources/Wpf.Ui.xaml", StringComparison.OrdinalIgnoreCase);

            if (isThemeOrControls)
                merged.RemoveAt(i);
        }

        // Ensure global dictionaries are always first (same order as UiApplication.Current.Resources).
        merged.Insert(0, globalTheme);
        merged.Insert(1, globalControls);
    }

    private void UpdateBackground(ApplicationTheme theme)
    {
        foreach (var window in _observedElements.Select(Window.GetWindow).Distinct())
        {
            WindowBackgroundManager.UpdateBackground(window, theme, WindowBackdropType.Mica);
        }
    }

    public ApplicationTheme GetEffectiveTheme()
    {
        var theme = settingsService.GeneralConfig.Theme;
#if REVIT2024_OR_GREATER
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (theme == ApplicationTheme.Auto)
            return GetRevitTheme();
#endif
        return theme;
    }

    private static ApplicationTheme GetFrameworkElementTheme(FrameworkElement frameworkElement)
    {
        var merged = frameworkElement.Resources.MergedDictionaries;
        return merged.Any(dictionary =>
                dictionary.Source is not null
                && dictionary.Source.OriginalString.Contains("Dark", StringComparison.OrdinalIgnoreCase))
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }
}