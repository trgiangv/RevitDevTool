using System.ComponentModel ;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Color = System.Windows.Media.Color;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
#endif

namespace RevitDevTool.Theme;

public sealed class ThemeWatcher
{
    public static readonly ThemeWatcher Instance = new();
    
#if REVIT2024_OR_GREATER
    private bool _isWatching;
#endif

    private readonly List<FrameworkElement> _observedElements = [];

    public void Initialize()
    {
        UiApplication.Current.Resources = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/RevitDevTool;component/Theme/Theme.xaml", UriKind.Absolute)
        };

        ApplicationThemeManager.Changed += OnApplicationThemeManagerChanged;
    }

    public void ApplyTheme()
    {
#if !REVIT2024_OR_GREATER
        var theme = ApplicationTheme.Dark;
#else
        var theme = GetRevitTheme();

        if (!_isWatching)
        {
            ExternalEventController.ActionEventHandler.Raise(application => application.ThemeChanged += OnRevitThemeChanged);
            _isWatching = true;
        }
#endif
        ApplicationThemeManager.Apply(theme);
        UpdateBackground(theme);
    }
    
      
#if REVIT2024_OR_GREATER
    public static void ApplyTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
        Instance.ApplyTheme();
    }
#endif

    public void Watch(FrameworkElement frameworkElement)
    {
        ApplicationThemeManager.Apply(frameworkElement);
        frameworkElement.Loaded += OnWatchedElementLoaded;
        frameworkElement.Unloaded += OnWatchedElementUnloaded;
    }

    public void Unwatch()
    {
#if REVIT2024_OR_GREATER
        if (!_isWatching) return;

        ExternalEventController.ActionEventHandler.Raise(application => application.ThemeChanged -= OnRevitThemeChanged);
        _isWatching = false;
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

        if (element.Resources.MergedDictionaries[0].Source.OriginalString != UiApplication.Current.Resources.MergedDictionaries[0].Source.OriginalString)
        {
            ApplicationThemeManager.Apply(element);
            UpdateDictionary(element);
        }
    }

    private void OnWatchedElementUnloaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement) sender;
        _observedElements.Remove(element);
    }

    private static void UpdateDictionary(FrameworkElement frameworkElement)
    {
        var themedResources = frameworkElement.Resources.MergedDictionaries
            .Where(dictionary => dictionary.Source.OriginalString.Contains("RevitDevTool.UI;", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        frameworkElement.Resources.MergedDictionaries.Insert(0, UiApplication.Current.Resources.MergedDictionaries[0]);
        frameworkElement.Resources.MergedDictionaries.Insert(1, UiApplication.Current.Resources.MergedDictionaries[1]);

        foreach (var themedResource in themedResources)
        {
            frameworkElement.Resources.MergedDictionaries.Remove(themedResource);
        }
    }

    private void UpdateBackground(ApplicationTheme theme)
    {
        foreach (var window in _observedElements.Select(Window.GetWindow).Distinct())
        {
            WindowBackgroundManager.UpdateBackground(window, theme, WindowBackdropType.Mica);
        }
    }
}