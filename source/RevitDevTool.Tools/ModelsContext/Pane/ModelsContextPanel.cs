using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Panel;
using DevTools.Presentation.Views.ModelsContext.Panel;
using Microsoft.Extensions.Logging;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Visibility;

namespace RevitDevTool.Tools.ModelsContext.Pane;

public sealed class ModelsContextPanel(
    ModelContextServices services,
    IRevitModelEvents events,
    ILogger<ModelsContextPane> logger)
{
    private ModelContextViewModel? _panel;

    public bool IsActive
    {
        set => events.IsActive = value;
    }

    public bool IsBuilt => _panel is not null;

    public void Start(UIControlledApplication application)
    {
        RevitProfiler.Use(logger);
        events.Subscribe(application);
        ViewActivations.Watch(application);
    }

    public ModelContextView Build()
    {
        using var timer = TraceTimer.Start(logger, "Panel build");
        _panel = new ModelContextViewModel(services) { IsPaneVisible = false };
        var view = new ModelContextView { DataContext = _panel };
        view.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "RegionBrush");
        return view;
    }

    public void Stop()
    {
        events.Unsubscribe();
        ViewActivations.Unwatch();
        _panel?.Dispose();
        _panel = null;
    }

    public static async Task DropFiltersAsync(Func<bool> isShown)
    {
        if (!LinkViewFilters.HasAnyAnywhere)
        {
            return;
        }

        try
        {
            await RevitProfiler.RunAsync("Drop view filters", application =>
            {
                if (!isShown())
                {
                    LinkViewFilters.RemoveEverywhere(application);
                }
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Drop view filters", "panel closed");
        }
    }
}
