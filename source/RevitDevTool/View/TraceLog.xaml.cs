using RevitDevTool.Theme;
using RevitDevTool.ViewModel;
using ApplicationTheme = UIFramework.ApplicationTheme;
#if REVIT2024_OR_GREATER
using System.ComponentModel;
using Autodesk.Revit.UI;
#endif

namespace RevitDevTool.View;

public partial class TraceLog : IDisposable
{
    public TraceLog()
    {
        InitializeComponent();
        DataContext = new TraceLogViewModel();
#if REVIT2024_OR_GREATER
        ApplicationTheme.CurrentTheme.PropertyChanged += ApplyTheme;
#endif
        ThemeWatcher.Instance.Initialize();
        ThemeWatcher.Instance.Watch(this);
        Loaded += (_, _) =>
        {
            if (DataContext is TraceLogViewModel vm)
                vm.RefreshTheme();
        };
    }

#if REVIT2024_OR_GREATER
    private static void ApplyTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
        ThemeWatcher.Instance.ApplyTheme();
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