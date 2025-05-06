using System.ComponentModel;
using System.Windows;
using Autodesk.Revit.UI;
using MaterialDesignThemes.Wpf;
using UIFramework;

namespace RevitDevTool.View;

public partial class TraceLog : IDisposable
{
    public TraceLog()
    {
        InitializeComponent();
#if REVIT2024_OR_GREATER
        ApplicationTheme.CurrentTheme.PropertyChanged += ApplyTheme;
#endif
    }

#if REVIT2024_OR_GREATER
    private void ApplyTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
    
        var resourceDictionary = GetThemeResourceDictionary();
        var materialDesignTheme = resourceDictionary.GetTheme();

        var newTheme = UIThemeManager.CurrentTheme switch
        {
            UITheme.Light => BaseTheme.Light,
            UITheme.Dark => BaseTheme.Dark,
            _ => throw new ArgumentOutOfRangeException()
        };
        materialDesignTheme.SetBaseTheme(newTheme);
        resourceDictionary.SetTheme(materialDesignTheme);
    }
    
    private ResourceDictionary GetThemeResourceDictionary()
    {
        return Resources.MergedDictionaries.First(x => x is IMaterialDesignThemeDictionary);
    }
#endif

    public void Dispose()
    {
#if REVIT2024_OR_GREATER
        ApplicationTheme.CurrentTheme.PropertyChanged -= ApplyTheme;
#endif
        Resources.MergedDictionaries.Clear();
        GC.SuppressFinalize(this);
    }
}