using System.Windows;
using RevitDevTool.Services ;
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
    
    private readonly List<FrameworkElement> _observedElements = [];

    public void Initialize()
    {
        UiApplication.Current.Resources = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/RevitDevTool;component/Theme/Theme.xaml", UriKind.Absolute)
        };

#if REVIT2024_OR_GREATER
        Context.UiApplication.ThemeChanged += Instance.OnRevitThemeChanged;
#endif
        ApplicationThemeManager.Changed += OnApplicationThemeManagerChanged;
    }

    public void Watch(FrameworkElement frameworkElement)
    {
        ApplicationThemeManager.Apply(frameworkElement);
        frameworkElement.Loaded += OnWatchedElementLoaded;
        frameworkElement.Unloaded += OnWatchedElementUnloaded;
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
        
        var requiredTheme = GetRequiredTheme();
        if (GetFrameworkElementTheme(element) == requiredTheme)
        {
            return;
        }
        
        ApplicationThemeManager.Apply(element);
        ApplicationThemeManager.Apply(requiredTheme);
    }

    private void OnWatchedElementUnloaded(object sender, RoutedEventArgs e)
    {
        var element = (FrameworkElement) sender;
        _observedElements.Remove(element);
    }

    public void ApplyTheme()
    {
        var theme = GetRequiredTheme();
        ApplicationThemeManager.Apply(theme);
        UpdateBackground(theme);
    }

    private static void UpdateDictionary(FrameworkElement frameworkElement)
    {
        var themedResources = frameworkElement.Resources.MergedDictionaries
            .Where(dictionary => dictionary.Source is not null && dictionary.Source.OriginalString.Contains("RevitDevTool.UI;", StringComparison.OrdinalIgnoreCase))
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
    
    public static ApplicationTheme GetRequiredTheme()
    {
#if !REVIT2024_OR_GREATER
        var theme = SettingsService.Instance.GeneralConfig.Theme;
#else
        var theme = SettingsService.Instance.GeneralConfig.Theme == ApplicationTheme.Auto 
            ? GetRevitTheme() 
            : SettingsService.Instance.GeneralConfig.Theme;
#endif
        return theme;
    }
    
    private static ApplicationTheme GetFrameworkElementTheme(FrameworkElement frameworkElement)
    {
        var resource = frameworkElement.Resources.MergedDictionaries ;
        return resource.Any(dictionary => dictionary.Source is not null && dictionary.Source.OriginalString.Contains("Dark", StringComparison.OrdinalIgnoreCase))
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
    }
}