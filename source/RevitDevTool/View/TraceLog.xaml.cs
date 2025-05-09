using RevitDevTool.Theme;
using ApplicationTheme = UIFramework.ApplicationTheme;
#if REVIT2024_OR_GREATER
using System.ComponentModel;
using Autodesk.Revit.UI;
#endif

namespace RevitDevTool.View;

public partial class TraceLog : IDisposable
{
    private static readonly ThemeWatcher ThemeWatcher = new();
    
    public TraceLog()
    {
        InitializeComponent();
#if REVIT2024_OR_GREATER
        ApplicationTheme.CurrentTheme.PropertyChanged += ApplyTheme;
#endif
        ThemeWatcher.Initialize();
        ThemeWatcher.Watch(this);
        ThemeWatcher.ApplyTheme();
    }

#if REVIT2024_OR_GREATER
    private static void ApplyTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
        ThemeWatcher.ApplyTheme();
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